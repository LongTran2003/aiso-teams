
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
                    $"Xin lỗi, mình chưa hiểu yêu cầu. ({dispatch.Reason})\n" +
                    "Thử gõ: \"show orders\" hoặc \"đơn hàng gần đây\"",
                    cancellationToken: cancellationToken);
                return;
            }

            if (dispatch.Result is not { Success: true } result)
            {
                _logger.LogWarning(
                    "Function {Function} returned failure: {Error}",
                    dispatch.FunctionName, dispatch.Result?.ErrorMessage);

                await turnContext.SendActivityAsync(
                    $"Function failed: {dispatch.Result?.ErrorMessage}",
                    cancellationToken: cancellationToken);
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
                        "Không có sales order nào phù hợp với truy vấn.",
                        cancellationToken: cancellationToken);
                    return;
                }

                var card = SoSummaryCardBuilder.Build(orders);
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(card), cancellationToken);

                _logger.LogInformation(
                    "Bot replied with Adaptive Card listing {Count} orders", orders.Count);
                return;
            }

            // Workflow action results (Release, Reject, Forward) — extract message field
            if (result.Payload is not null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(result.Payload);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
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

    private static Attachment BuildWelcomeCard(string username)
    {
        var templateJson = CardTemplateFileLoader.LoadFromFrontendCards("welcome.json");
        var template = new AdaptiveCardTemplate(templateJson);
        var cardJson = template.Expand(new { username });

        return new Attachment
        {
            ContentType = "application/vnd.microsoft.card.adaptive",
            Content = JsonConvert.DeserializeObject(cardJson)
        };
    }

    private static Attachment BuildHelpCard()
    {
        var templateJson = CardTemplateFileLoader.LoadFromFrontendCards("help.json");
        var template = new AdaptiveCardTemplate(templateJson);
        var cardJson = template.Expand(new { });

        return new Attachment
        {
            ContentType = "application/vnd.microsoft.card.adaptive",
            Content = JsonConvert.DeserializeObject(cardJson)
        };
    }

    private static string DeriveStatus(DispatchResult d)
    {
        if (!d.Handled) return "Unrecognized";
        if (d.Result is null) return "Failed";
        return d.Result.Success ? "Success" : "Failed";
    }
}
