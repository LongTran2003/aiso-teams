using AISO.Bot.Cards.Builders;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace AISO.Bot;

public class AdapterWithErrorHandler : CloudAdapter
{
    public AdapterWithErrorHandler(
        BotFrameworkAuthentication auth,
        ILogger<IBotFrameworkHttpAdapter> logger)
        : base(auth, logger)
    {
        OnTurnError = async (turnContext, exception) =>
        {
            logger.LogError(exception, "[OnTurnError] unhandled error: {Message}", exception.Message);

            try
            {
                var errorCard = BuildUnhandledErrorCard(exception);
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(errorCard), default);
            }
            catch
            {
                await turnContext.SendActivityAsync(
                    "Unexpected bot error. Please try again.");

                if (turnContext.Activity.ChannelId == "msteams")
                {
                    await turnContext.SendActivityAsync(
                        "To continue, restart the conversation or contact your admin.");
                }
            }
        };
    }

    private static Attachment BuildUnhandledErrorCard(Exception exception)
    {
        var isAuthError = exception.Message.Contains("auth", StringComparison.OrdinalIgnoreCase)
                       || exception.Message.Contains("token", StringComparison.OrdinalIgnoreCase)
                       || exception.Message.Contains("sign", StringComparison.OrdinalIgnoreCase)
                       || exception.Message.Contains("401", StringComparison.OrdinalIgnoreCase);

        if (isAuthError)
        {
            return TeamsCardBuilder.BuildErrorCard(
                "UNAUTHENTICATED",
                exception.Message);
        }

        return TeamsCardBuilder.BuildErrorCard(
            "UNHANDLED",
            exception.Message);
    }
}
