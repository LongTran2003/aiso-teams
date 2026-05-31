using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace AISO.Bot;

public class TeamsBot : ActivityHandler
{
    protected override async Task OnMessageActivityAsync(
        ITurnContext<IMessageActivity> turnContext,
        CancellationToken cancellationToken)
    {
        var userMessage = turnContext.Activity.Text;
        var reply = $"AISO Bot received: \"{userMessage}\"";
        await turnContext.SendActivityAsync(
            MessageFactory.Text(reply),
            cancellationToken);
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
                    MessageFactory.Text("Welcome to AISO-Teams Bot! Try sending me a message."),
                    cancellationToken);
            }
        }
    }
}