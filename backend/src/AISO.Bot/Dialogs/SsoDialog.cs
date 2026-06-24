using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AISO.Bot.Services;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace AISO.Bot.Dialogs;

public class SsoDialog : ComponentDialog
{
    private readonly ILogger<SsoDialog> _logger;
    private readonly UserMappingService _userMappingService;

    public SsoDialog(IConfiguration configuration, ILogger<SsoDialog> logger, UserMappingService userMappingService)
        : base(nameof(SsoDialog))
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
                Timeout = 300000, // User has 5 minutes to login
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
        return await stepContext.BeginDialogAsync(nameof(OAuthPrompt), new PromptOptions
        {
            RetryPrompt = MessageFactory.Text("Đăng nhập không thành công. Vui lòng thử lại hoặc gõ 'cancel' để thoát.")
        }, cancellationToken);
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

                // Decode token to get email or name
                var displayName = stepContext.Context.Activity.From.Name ?? "Unknown User";
                try
                {
                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    var jwt = handler.ReadJwtToken(tokenResponse.Token);
                    displayName = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? displayName;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decode JWT token to extract display name.");
                }
                stepContext.Values["DisplayName"] = displayName;

                var sapUsername = await _userMappingService.GetSapUsernameAsync(teamsId, cancellationToken);
                if (string.IsNullOrEmpty(sapUsername))
                {
                    // User is not mapped, prompt them to enter their SAP username
                    return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
                    {
                        Prompt = MessageFactory.Text("Bạn đã đăng nhập thành công vào hệ thống. Tuy nhiên, chúng tôi chưa biết User ID trên SAP của bạn là gì. Vui lòng nhập SAP Username (ví dụ: LONGTNQ):")
                    }, cancellationToken);
                }

                // Already mapped, pass the sapUsername to the final step
                return await stepContext.NextAsync(sapUsername, cancellationToken);
            }
        }

        await stepContext.Context.SendActivityAsync(MessageFactory.Text("Đăng nhập thất bại. Vui lòng thử lại."), cancellationToken);
        return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
    }

    private async Task<DialogTurnResult> EnsureSapMappingStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        var teamsId = stepContext.Context.Activity.From.Id;
        var displayName = (stepContext.Values.TryGetValue("DisplayName", out var e) ? e?.ToString() : null) ?? "Unknown User";

        // If the result is a string, it means we came from the TextPrompt (user entered SAP Username)
        if (stepContext.Result is string sapUsernameInput && !string.IsNullOrEmpty(sapUsernameInput))
        {
            var sapUsername = sapUsernameInput.Trim().ToUpper();
            await _userMappingService.MapUserAsync(teamsId, displayName, sapUsername, cancellationToken);
            await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Đã liên kết tài khoản Teams của bạn với SAP User: **{sapUsername}**."), cancellationToken);
            return await stepContext.NextAsync(sapUsername, cancellationToken);
        }

        // If the result is not a string, it was passed from the previous step (user was already mapped)
        return await stepContext.NextAsync(stepContext.Result, cancellationToken);
    }

    private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        var sapUsername = stepContext.Result as string;

        // Return the sapUsername to the caller
        return await stepContext.EndDialogAsync(sapUsername, cancellationToken);
    }
}
