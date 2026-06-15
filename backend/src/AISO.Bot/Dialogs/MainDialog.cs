using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AISO.Bot.Services;

namespace AISO.Bot.Dialogs;

public class MainDialog : ComponentDialog
{
    private readonly ILogger _logger;
    private readonly UserMappingService _userMappingService;

    public MainDialog(IConfiguration configuration, ILogger<MainDialog> logger, UserMappingService userMappingService)
        : base(nameof(MainDialog))
    {
        _logger = logger;
        _userMappingService = userMappingService;

        var connectionName = configuration["BotSso:ConnectionName"] ?? "AisoTeamsSsoConnection";

        AddDialog(new OAuthPrompt(
            nameof(OAuthPrompt),
            new OAuthPromptSettings
            {
                ConnectionName = connectionName,
                Text = "Vui lòng đăng nhập để sử dụng tính năng của bot.",
                Title = "Đăng nhập",
                Timeout = 300000 // User has 5 minutes to login
            }));

        AddDialog(new TextPrompt(nameof(TextPrompt)));
        
        AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
        {
            PromptStepAsync,
            LoginStepAsync,
            EnsureSapMappingStepAsync,
            FinalStepAsync
        }));

        InitialDialogId = nameof(WaterfallDialog);
    }

    private async Task<DialogTurnResult> PromptStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        return await stepContext.BeginDialogAsync(nameof(OAuthPrompt), null, cancellationToken);
    }

    private async Task<DialogTurnResult> LoginStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        // Get the token from the previous step
        if (stepContext.Result != null)
        {
            var tokenResponse = (TokenResponse)stepContext.Result;
            if (tokenResponse?.Token != null)
            {
                // We have the token! Now check if user is mapped.
                var teamsId = stepContext.Context.Activity.From.Id;
                
                // Decode token to get email
                var email = "unknown@domain.com";
                try
                {
                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    var jwt = handler.ReadJwtToken(tokenResponse.Token);
                    email = jwt.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value ?? email;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decode JWT token to extract email.");
                }
                stepContext.Values["TeamsEmail"] = email;

                var sapUsername = await _userMappingService.GetSapUsernameAsync(teamsId, cancellationToken);
                if (string.IsNullOrEmpty(sapUsername))
                {
                    // User is not mapped, prompt them to enter their SAP username
                    return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
                    {
                        Prompt = MessageFactory.Text("Bạn đã đăng nhập thành công vào Entra ID. Tuy nhiên, hệ thống chưa biết User ID trên SAP của bạn là gì. Vui lòng nhập SAP Username (ví dụ: LONGTNQ):")
                    }, cancellationToken);
                }

                // Already mapped, pass the token and sapUsername to the final step
                return await stepContext.NextAsync(sapUsername, cancellationToken);
            }
        }

        await stepContext.Context.SendActivityAsync(MessageFactory.Text("Đăng nhập thất bại. Vui lòng thử lại."), cancellationToken);
        return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
    }

    private async Task<DialogTurnResult> EnsureSapMappingStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        var teamsId = stepContext.Context.Activity.From.Id;
        var email = (stepContext.Values.TryGetValue("TeamsEmail", out var e) ? e?.ToString() : null) ?? "unknown@domain.com";

        // If the result is a string, it means we came from the TextPrompt (user entered SAP Username)
        if (stepContext.Result is string sapUsernameInput)
        {
            var sapUsername = sapUsernameInput.Trim().ToUpper();
            await _userMappingService.MapUserAsync(teamsId, email, sapUsername, cancellationToken);
            await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Đã liên kết tài khoản Teams của bạn với SAP User: **{sapUsername}**."), cancellationToken);
            return await stepContext.NextAsync(sapUsername, cancellationToken);
        }

        // If the result is not a string, it was passed from the previous step (user was already mapped)
        return await stepContext.NextAsync(stepContext.Result, cancellationToken);
    }

    private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        var sapUsername = stepContext.Result as string;
        
        // Return the sapUsername to the Bot
        return await stepContext.EndDialogAsync(sapUsername, cancellationToken);
    }
}
