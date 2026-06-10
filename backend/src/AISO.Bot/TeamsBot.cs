
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

namespace AISO.Bot;

public class TeamsBot : ActivityHandler
{
    private readonly IFunctionDispatcher _dispatcher;
    private readonly IAuditLogger _audit;
    private readonly ILogger<TeamsBot> _logger;

    public TeamsBot(
        IFunctionDispatcher dispatcher,
        IAuditLogger audit,
        ILogger<TeamsBot> logger)
    {
        _dispatcher = dispatcher;
        _audit = audit;
        _logger = logger;
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

            var stopwatch = Stopwatch.StartNew();
            var dispatch = await _dispatcher.DispatchAsync(userMessage, cancellationToken);
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

            if (result.Payload is IReadOnlyList<SalesOrder> orders)
            {
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

            await turnContext.SendActivityAsync(
                $"Function {dispatch.FunctionName} executed (no renderer for payload type).",
                cancellationToken: cancellationToken);
        }
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
