using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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
                // Build a user-friendly Adaptive Card with error info
                var errorCard = BuildErrorCard(exception);
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(errorCard), default);
            }
            catch
            {
                // If card building fails, fallback to plain text
                await turnContext.SendActivityAsync(
                    "Bot gặp lỗi không mong muốn. Vui lòng thử lại.");

                if (turnContext.Activity.ChannelId == "msteams")
                {
                    await turnContext.SendActivityAsync(
                        "Để tiếp tục, vui lòng restart conversation hoặc liên hệ admin.");
                }
            }
        };
    }

    private static Attachment BuildErrorCard(Exception exception)
    {
        var isAuthError = exception.Message.Contains("auth", StringComparison.OrdinalIgnoreCase)
                       || exception.Message.Contains("token", StringComparison.OrdinalIgnoreCase)
                       || exception.Message.Contains("sign", StringComparison.OrdinalIgnoreCase)
                       || exception.Message.Contains("401", StringComparison.OrdinalIgnoreCase);

        var title = isAuthError
            ? "🔑 Phiên đăng nhập hết hạn"
            : "⚠️ Đã xảy ra lỗi hệ thống";

        var message = isAuthError
            ? "Phiên làm việc của bạn đã hết hạn hoặc chưa được xác thực. Vui lòng gõ bất kỳ tin nhắn nào để đăng nhập lại."
            : "AISO Bot không thể xử lý yêu cầu này. Vui lòng thử lại sau hoặc liên hệ admin.";

        var cardJson = $@"{{
  ""type"": ""AdaptiveCard"",
  ""$schema"": ""http://adaptivecards.io/schemas/adaptive-card.json"",
  ""version"": ""1.5"",
  ""body"": [
    {{
      ""type"": ""Container"",
      ""style"": ""attention"",
      ""bleed"": true,
      ""items"": [
        {{
          ""type"": ""TextBlock"",
          ""text"": ""{EscapeJson(title)}"",
          ""weight"": ""Bolder"",
          ""size"": ""Medium"",
          ""color"": ""Attention""
        }}
      ]
    }},
    {{
      ""type"": ""TextBlock"",
      ""text"": ""{EscapeJson(message)}"",
      ""wrap"": true,
      ""spacing"": ""Medium""
    }},
    {{
      ""type"": ""TextBlock"",
      ""text"": ""💡 Gợi ý: Gõ **help** để xem danh sách lệnh, hoặc gõ bất kỳ tin nhắn nào để đăng nhập lại."",
      ""wrap"": true,
      ""isSubtle"": true,
      ""size"": ""Small"",
      ""spacing"": ""Large""
    }}
  ]
}}";

        return new Attachment
        {
            ContentType = "application/vnd.microsoft.card.adaptive",
            Content = JsonConvert.DeserializeObject(cardJson)
        };
    }

    private static string EscapeJson(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
