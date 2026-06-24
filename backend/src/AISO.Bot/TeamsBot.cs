
using System.Diagnostics;
using AdaptiveCards.Templating;
using AISO.AiOrchestration;
using AISO.Bot.Cards;
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
        var userMessage = turnContext.Activity.Text ?? string.Empty;

        // If Text is empty but we have Value (e.g. from an Adaptive Card Action.Submit button)
        if (string.IsNullOrWhiteSpace(userMessage) && turnContext.Activity.Value != null)
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
                            MessageFactory.Attachment(BuildSalesOrderDetailCard(new
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
                            MessageFactory.Attachment(BuildKpiRevenueCard(new
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
                            MessageFactory.Attachment(BuildKpiDeliveryCard(new
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
                    MessageFactory.Attachment(BuildHelpCard()),
                    cancellationToken);
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
                MessageFactory.Attachment(BuildLoadingCard()),
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
                    ParametersJson = "{}",
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
                    MessageFactory.Attachment(BuildErrorCard("UNHANDLED", dispatch.Reason ?? "Unknown request")),
                    cancellationToken);
                return;
            }

            if (dispatch.Result is not { Success: true } result)
            {
                _logger.LogWarning(
                    "Function {Function} returned failure: {Error}",
                    dispatch.FunctionName, dispatch.Result?.ErrorMessage);

                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(BuildErrorCard("FUNCTION_FAILED", dispatch.Result?.ErrorMessage ?? "Unknown error")),
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
                        MessageFactory.Attachment(BuildEmptyCard()),
                        cancellationToken);
                    return;
                }

                var kpiCard = BuildKpiCardForRequest(normalizedMessage, orders, getOrdersResponse.ChartUrl);
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

                var card = SoSummaryCardBuilder.Build(orders);
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(card), cancellationToken);

                _logger.LogInformation(
                    "Bot replied with Adaptive Card listing {Count} orders", orders.Count);
                return;
            }

            // Workflow action results (Release, Reject, Forward) — show a success card when applicable
            if (result.Payload is not null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(result.Payload);
                using var doc = System.Text.Json.JsonDocument.Parse(json);

                if (TryBuildWorkflowSuccessCard(doc.RootElement, dispatch.FunctionName, out var workflowCard))
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Attachment(workflowCard),
                        cancellationToken);

                    _logger.LogInformation(
                        "Bot replied with workflow success card for {Function}", dispatch.FunctionName);
                    return;
                }

                var message = doc.RootElement.TryGetProperty("message", out var msg)
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

        if (turnContext.Activity.Name == "signin/verifyState" || turnContext.Activity.Name == "signin/tokenExchange")
        {
            _logger.LogInformation("Received SSO Token Exchange Invoke Activity");
            await _dialog.RunAsync(turnContext, _conversationState.CreateProperty<DialogState>("DialogState"), cancellationToken);
            return new InvokeResponse { Status = 200 };
        }

        // When silent SSO fails, Teams sends "signin/failure". We MUST return 200 to tell Teams to show the Sign-in button.
        if (turnContext.Activity.Name == "signin/failure")
        {
            _logger.LogWarning("SSO Token Exchange failed. Teams should now fallback to showing the OAuthCard.");
            return new InvokeResponse { Status = 200 };
        }

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
                        BuildWelcomeCard(member.Name ?? "bạn")),
                    cancellationToken);
            }
        }
    }

    private static Attachment BuildWelcomeCard(string username) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("welcome.json", new { username });

    private static Attachment BuildHelpCard() =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("help.json");

    private static Attachment BuildEmptyCard() =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("empty.json");

    private static Attachment BuildLoadingCard() =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("loading.json");

    private static Attachment BuildSuccessCard(string salesOrderNumber, string status) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("success.json", new { salesOrderNumber, status });

    private static Attachment BuildErrorCard(string errorCode, string errorMessage) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("error.json", new { errorCode, errorMessage });

    private static Attachment BuildConfirmRejectCard(string salesOrderNumber) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("confirm-reject.json", new { salesOrderNumber });

    private static Attachment BuildKpiSummaryCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("kpi-summary.json", data);

    private static Attachment BuildKpiRevenueCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("kpi-revenue.json", data);

    private static Attachment BuildKpiDeliveryCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("kpi-delivery.json", data);

    private static Attachment BuildSalesOrderDetailCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("sales-order-detail.json", data);

    private static Attachment? BuildKpiCardForRequest(string message, IReadOnlyList<SalesOrder> orders, string? chartUrl)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var lowerMessage = message.ToLowerInvariant();
        var currency = orders.FirstOrDefault()?.Currency ?? "USD";
        var totalRevenue = orders.Sum(o => o.NetValue);
        var targetRevenue = totalRevenue + Math.Max(10000m, totalRevenue * 0.1m);

        if (lowerMessage.Contains("delivery"))
        {
            var deliveredCount = orders.Count(o => o.Status is SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced);
            var delayedCount = orders.Count(o => o.Status is SalesOrderStatus.Blocked or SalesOrderStatus.PartiallyDelivered or SalesOrderStatus.Open);
            var onTimeRate = orders.Count == 0 ? 0 : Math.Round((double)deliveredCount / orders.Count * 100, 0);

            return BuildKpiDeliveryCard(new
            {
                onTimeRate = $"{onTimeRate}%",
                delayedCount = delayedCount.ToString(),
                completedToday = deliveredCount.ToString(),
                deliveryProgress = (int)onTimeRate,
                chartUrl = chartUrl
            });
        }

        if (lowerMessage.Contains("revenue"))
        {
            return BuildKpiRevenueCard(new
            {
                period = "Current results",
                totalRevenue = $"{totalRevenue:N0} {currency}",
                growthRate = orders.Count > 5 ? "+12%" : "+8%",
                targetRevenue = $"{targetRevenue:N0} {currency}",
                chartUrl = chartUrl
            });
        }

        if (lowerMessage.Contains("kpi") || lowerMessage.Contains("summary"))
        {
            return BuildKpiSummaryCard(new
            {
                revenueValue = $"{totalRevenue:N0} {currency}",
                orderCount = orders.Count,
                chartUrl = chartUrl
            });
        }

        return null;
    }

    private static bool TryBuildWorkflowSuccessCard(System.Text.Json.JsonElement payload, string? functionName, out Attachment? card)
    {
        card = null;
        if (payload.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return false;
        }

        if (!payload.TryGetProperty("order_id", out var orderIdElement) || orderIdElement.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(orderIdElement.GetString()))
        {
            return false;
        }

        var action = payload.TryGetProperty("action", out var actionElement) && actionElement.ValueKind == System.Text.Json.JsonValueKind.String
            ? actionElement.GetString()
            : functionName;

        card = BuildSuccessCard(orderIdElement.GetString()!, action ?? "Completed");
        return true;
    }

    private static string DeriveStatus(DispatchResult d)
    {
        if (!d.Handled) return "Unrecognized";
        if (d.Result is null) return "Failed";
        return d.Result.Success ? "Success" : "Failed";
    }
}
