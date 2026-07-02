
using System.Diagnostics;
using AISO.AiOrchestration;
using AISO.Bot.Cards;
using AISO.Bot.Cards.Builders;
using AISO.Domain.SalesOrders;
using AISO.Persistence.Auditing;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog.Context;

using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Teams;
using AISO.Bot.Dialogs;
using AISO.Bot.Services;

namespace AISO.Bot;

public class TeamsBot : TeamsActivityHandler
{
    private readonly IFunctionDispatcher _dispatcher;
    private readonly IAuditLogger _audit;
    private readonly ILogger<TeamsBot> _logger;
    private readonly ConversationState _conversationState;
    private readonly UserState _userState;
    private readonly SsoDialog _dialog;
    private readonly UserMappingService _userMappingService;

    public TeamsBot(
        IFunctionDispatcher dispatcher,
        IAuditLogger audit,
        ILogger<TeamsBot> logger,
        ConversationState conversationState,
        UserState userState,
        SsoDialog dialog,
        UserMappingService userMappingService)
    {
        _dispatcher = dispatcher;
        _audit = audit;
        _logger = logger;
        _conversationState = conversationState;
        _userState = userState;
        _dialog = dialog;
        _userMappingService = userMappingService;
    }

    public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
    {
        await base.OnTurnAsync(turnContext, cancellationToken);

        // Save any state changes that might have occurred during the turn.
        await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
        await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
    }

    protected override async Task OnMessageActivityAsync(
        ITurnContext<IMessageActivity> turnContext,
        CancellationToken cancellationToken)
    {
        var userMessage = turnContext.Activity.RemoveRecipientMention() ?? turnContext.Activity.Text ?? string.Empty;
        userMessage = System.Text.RegularExpressions.Regex.Replace(userMessage, "<[^>]*>", string.Empty);
        
        // When an Adaptive Card button (Action.Submit with msteams.type=messageBack) is clicked,
        // Teams sends BOTH Activity.Text (= button title, e.g. "view order 129998")
        // AND Activity.Value (= the data payload, e.g. { action: "view_details" }).
        // We MUST check Value first so the structured command wins over the display title.
        if (turnContext.Activity.Value != null)
        {
            try
            {
                var valueObj = Newtonsoft.Json.Linq.JObject.FromObject(turnContext.Activity.Value);
                if (valueObj.TryGetValue("command", StringComparison.OrdinalIgnoreCase, out var cmdToken))
                {
                    userMessage = cmdToken.ToString();
                }
                else if (valueObj.TryGetValue("action", StringComparison.OrdinalIgnoreCase, out var actionToken))
                {
                    var action = actionToken.ToString();
                    if (string.Equals(action, "view_details", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken)
                            ? idToken.ToString()
                            : "UNKNOWN";

                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildSalesOrderDetailCard(new
                            {
                                salesOrderNumber = salesOrderId,
                                customerName = "Sample Customer",
                                customerId = "1000",
                                documentDate = DateTime.Now.ToString("dd MMM yyyy"),
                                netAmount = "$12,500",
                                currency = "USD",
                                approvalStatus = "Pending"
                            })),
                            cancellationToken);
                        return;
                    }

                    if (string.Equals(action, "view_revenue_kpi", StringComparison.OrdinalIgnoreCase))
                    {
                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildKpiRevenueCard(new
                            {
                                period = "This month",
                                totalRevenue = "$245K",
                                growthRate = "+12%",
                                targetRevenue = "$220K",
                                chartUrl = "https://quickchart.io/chart?c=eyJ0eXBlIjoiZG91Z2hudXQiLCJkYXRhIjp7ImxhYmVscyI6WyJNYXJjaCJdLCJkYXRhc2V0cyI6W3siZGF0YSI6WzI0NSJdfV19"
                            })),
                            cancellationToken);
                        return;
                    }

                    if (string.Equals(action, "view_delivery_kpi", StringComparison.OrdinalIgnoreCase))
                    {
                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildKpiDeliveryCard(new
                            {
                                onTimeRate = "94%",
                                delayedCount = "12",
                                completedToday = "24",
                                deliveryProgress = 94,
                                chartUrl = "https://quickchart.io/chart?c=eyJ0eXBlIjoicG9sYXIiLCJkYXRhIjp7ImxhYmVscyI6WyJQcm9ncmVzcyJdLCJkYXRhc2V0cyI6W3siZGF0YSI6Wzk0XX1dfQ=="
                            })),
                            cancellationToken);
                        return;
                    }
                }
            }
            catch { /* Ignore parsing errors, userMessage stays empty */ }
        }

        var normalizedMessage = userMessage.Trim();
        var teamsUserId = turnContext.Activity.From?.Id ?? "anonymous";
        var conversationId = turnContext.Activity.Conversation?.Id;
        var activityId = turnContext.Activity.Id;

        // Push activity-scoped properties into Serilog LogContext so every
        // log emitted inside this turn is tagged for end-to-end traceability.
        using (LogContext.PushProperty("ActivityId", activityId))
        using (LogContext.PushProperty("ConversationId", conversationId))
        using (LogContext.PushProperty("UserId", teamsUserId))
        {
            _logger.LogInformation(
                "Bot received message: {UserMessage}", userMessage);

            if (string.Equals(normalizedMessage, "help", StringComparison.OrdinalIgnoreCase))
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(TeamsCardBuilder.BuildHelpCard()),
                    cancellationToken);
                return;
            }

            if (string.Equals(normalizedMessage, "cancel", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(normalizedMessage, "thoát", StringComparison.OrdinalIgnoreCase))
            {
                await _conversationState.ClearStateAsync(turnContext, cancellationToken);
                await turnContext.SendActivityAsync("Đã huỷ các tiến trình đang chạy. Bạn có thể bắt đầu lại.", cancellationToken: cancellationToken);
                return;
            }

            if (string.Equals(normalizedMessage, "logout", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedMessage, "đăng xuất", StringComparison.OrdinalIgnoreCase))
            {
                await _userMappingService.RemoveMappingAsync(teamsUserId, cancellationToken);
                await turnContext.SendActivityAsync("Đã đăng xuất tài khoản SAP thành công. Bạn có thể gõ 'hi' để thử đăng nhập lại.", cancellationToken: cancellationToken);
                return;
            }

            if (TryHandleOrderDetailRequest(normalizedMessage, turnContext, cancellationToken))
            {
                return;
            }

            var sapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);

            var dialogSet = new DialogSet(_conversationState.CreateProperty<DialogState>("DialogState"));
            dialogSet.Add(_dialog);
            var dialogContext = await dialogSet.CreateContextAsync(turnContext, cancellationToken);

            if (dialogContext.ActiveDialog != null || string.IsNullOrEmpty(sapUsername))
            {
                // We are either in the middle of login/mapping OR we need to start it
                await _dialog.RunAsync(turnContext, _conversationState.CreateProperty<DialogState>("DialogState"), cancellationToken);
                return;
            }

            await turnContext.SendActivityAsync(
                MessageFactory.Attachment(TeamsCardBuilder.BuildLoadingCard()),
                cancellationToken);

            var stopwatch = Stopwatch.StartNew();
            var dispatch = await _dispatcher.DispatchAsync(userMessage, sapUsername, cancellationToken);
            stopwatch.Stop();

            // Audit — best-effort: a write failure must not break the bot.
            try
            {
                await _audit.LogAsync(new AuditEntry
                {
                    TeamsUserId = teamsUserId,
                    ConversationId = conversationId,
                    Action = dispatch.FunctionName ?? "unrecognized",
                    ParametersJson = dispatch.ParametersJson,
                    ResultStatus = DeriveStatus(dispatch),
                    DurationMs = (int)stopwatch.ElapsedMilliseconds,
                    ErrorMessage = dispatch.Result?.ErrorMessage ?? dispatch.Reason
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write audit log entry");
            }

            if (!dispatch.Handled)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("UNHANDLED", dispatch.Reason ?? "Unknown request")),
                    cancellationToken);
                return;
            }

            if (dispatch.Result is not { Success: true } result)
            {
                _logger.LogWarning(
                    "Function {Function} returned failure: {Error}",
                    dispatch.FunctionName, dispatch.Result?.ErrorMessage);

                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("FUNCTION_FAILED", dispatch.Result?.ErrorMessage ?? "Unknown error")),
                    cancellationToken);
                return;
            }

            if (result.Payload is string textReply)
            {
                await turnContext.SendActivityAsync(
                    textReply,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Bot replied with AI text response");
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.GetSalesOrdersResponse getOrdersResponse)
            {
                var orders = getOrdersResponse.Orders;
                if (orders.Count == 0)
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Attachment(TeamsCardBuilder.BuildEmptyCard()),
                        cancellationToken);
                    return;
                }

                var kpiCard = TeamsCardBuilder.BuildKpiCardForRequest(normalizedMessage, orders, getOrdersResponse.ChartUrl);
                if (kpiCard is not null)
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Attachment(kpiCard),
                        cancellationToken);

                    _logger.LogInformation(
                        "Bot replied with KPI card for request '{Request}' using data from GetSalesOrdersFunction",
                        normalizedMessage);
                    return;
                }

                var card = TeamsCardBuilder.BuildSoSummaryCard(orders);
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(card), cancellationToken);

                _logger.LogInformation(
                    "Bot replied with Adaptive Card listing {Count} orders", orders.Count);
                return;
            }

            if (result.Payload is IReadOnlyList<Domain.SalesOrders.SalesOrder> ordersList)
            {
                if (ordersList.Count == 0)
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Attachment(TeamsCardBuilder.BuildEmptyCard()),
                        cancellationToken);
                    return;
                }

                var card = TeamsCardBuilder.BuildSoSummaryCard(ordersList);
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(card), cancellationToken);

                _logger.LogInformation(
                    "Bot replied with Adaptive Card listing {Count} orders from CheckOrderStatus", ordersList.Count);
                return;
            }

            // Workflow action results (Release, Reject, Forward) — show a success card when applicable
            if (result.Payload is not null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(result.Payload);
                using var doc = System.Text.Json.JsonDocument.Parse(json);

                if (TeamsCardBuilder.TryBuildWorkflowSuccessCard(doc.RootElement, dispatch.FunctionName, out var workflowCard))
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Attachment(workflowCard),
                        cancellationToken);

                    _logger.LogInformation(
                        "Bot replied with workflow success card for {Function}", dispatch.FunctionName);
                    return;
                }

                var message = doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && doc.RootElement.TryGetProperty("message", out var msg)
                    ? msg.GetString()
                    : $"✅ Function {dispatch.FunctionName} executed successfully.";

                await turnContext.SendActivityAsync(
                    message, cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "Bot replied with action result for {Function}", dispatch.FunctionName);
                return;
            }

            await turnContext.SendActivityAsync(
                $"Function {dispatch.FunctionName} executed (no result).",
                cancellationToken: cancellationToken);
        }
    }

    protected override async Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received Invoke Activity with Name: {InvokeName}", turnContext.Activity.Name);

        // SsoDialog no longer uses OAuthPrompt, so we don't need to handle
        // signin/verifyState or signin/tokenExchange here anymore.
        return await base.OnInvokeActivityAsync(turnContext, cancellationToken);
    }

    protected override async Task OnMembersAddedAsync(
        IList<ChannelAccount> membersAdded,
        ITurnContext<IConversationUpdateActivity> turnContext,
        CancellationToken cancellationToken)
    {
        foreach (var member in membersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(
                        TeamsCardBuilder.BuildWelcomeCard(member.Name ?? "bạn")),
                    cancellationToken);
            }
        }
    }

    private static string DeriveStatus(DispatchResult d)
    {
        if (!d.Handled) return "Unrecognized";
        if (d.Result is null) return "Failed";
        return d.Result.Success ? "Success" : "Failed";
    }

    private static bool TryHandleOrderDetailRequest(string message, ITurnContext turnContext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var lowered = message.ToLowerInvariant();
        var isDetailRequest = lowered.Contains("detail") || lowered.Contains("chi tiết") || lowered.Contains("xem chi tiết") || lowered.Contains("show detail");
        var mentionsOrder = lowered.Contains("order") || lowered.Contains("đơn hàng") || lowered.Contains("so") || lowered.Contains("sales order");

        if (!isDetailRequest || !mentionsOrder)
        {
            return false;
        }

        var match = System.Text.RegularExpressions.Regex.Match(message, @"(?:order|so|sales order|đơn hàng|đơn)\s*(?:no\.?|number|#)?\s*([A-Za-z0-9\-\/]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var orderId = match.Success ? match.Groups[1].Value : "UNKNOWN";

        turnContext.SendActivityAsync(
            MessageFactory.Attachment(TeamsCardBuilder.BuildSalesOrderDetailCard(new
            {
                salesOrderNumber = orderId,
                customerName = "Sample Customer",
                customerId = "1000",
                documentDate = DateTime.Now.ToString("dd MMM yyyy"),
                netAmount = "$12,500",
                currency = "USD",
                approvalStatus = "Pending"
            })),
            cancellationToken);
        return true;
    }
}
