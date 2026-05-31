using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
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

            await turnContext.SendActivityAsync(
                "Bot gặp lỗi không mong muốn. Vui lòng thử lại.");
            
            if (turnContext.Activity.ChannelId == "msteams")
            {
                await turnContext.SendActivityAsync(
                    "Để tiếp tục, vui lòng restart conversation hoặc liên hệ admin.");
            }
        };
    }
}