using AISO.AiOrchestration;
using AISO.Bot.Cards;
using AISO.Domain.SalesOrders;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace AISO.Bot;

public class TeamsBot : ActivityHandler
{
    private readonly IFunctionDispatcher _dispatcher;

    public TeamsBot(IFunctionDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    protected override async Task OnMessageActivityAsync(
        ITurnContext<IMessageActivity> turnContext,
        CancellationToken cancellationToken)
    {
        var userMessage = turnContext.Activity.Text ?? string.Empty;

        var dispatch = await _dispatcher.DispatchAsync(userMessage, cancellationToken);

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
            await turnContext.SendActivityAsync(
                $"Function failed: {dispatch.Result?.ErrorMessage}",
                cancellationToken: cancellationToken);
            return;
        }

        // Render result based on payload type
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
            return;
        }

        // Fallback for unsupported payload types
        await turnContext.SendActivityAsync(
            $"Function {dispatch.FunctionName} executed (no renderer for payload type).",
            cancellationToken: cancellationToken);
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
                    MessageFactory.Text("Welcome to AISO-Teams Bot! Try: \"show orders\""),
                    cancellationToken);
            }
        }
    }
}
