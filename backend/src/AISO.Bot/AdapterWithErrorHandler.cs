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

            var isColdStart = exception is OperationCanceledException;

            try
            {
                var errorCard = BuildUnhandledErrorCard(exception);
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(errorCard), default);
            }
            catch
            {
                // Cold-start or already-cancelled context: try a plain text message.
                // Some failures won't even allow SendActivity — best-effort only.
                try
                {
                    await turnContext.SendActivityAsync(
                        isColdStart
                            ? "The bot is starting up. Please send your message again in a moment."
                            : "Unexpected bot error. Please try again.",
                        cancellationToken: default);
                }
                catch
                {
                    // Give up gracefully — Teams will show its own fallback message.
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

        // Cold-start race: App Service just woke up, SAP TLS handshake, JIT,
        // or first request after deploy. The very next message usually works.
        if (exception is OperationCanceledException)
        {
            return TeamsCardBuilder.BuildErrorCard(
                "COLD_START",
                exception.Message);
        }

        return TeamsCardBuilder.BuildErrorCard(
            "UNHANDLED",
            exception.Message);
    }
}
