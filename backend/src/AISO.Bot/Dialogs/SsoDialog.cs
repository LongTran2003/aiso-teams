using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Extensions.Logging;
using AISO.Bot.Services;
using System.Threading;
using System.Threading.Tasks;

namespace AISO.Bot.Dialogs;

/// <summary>
/// Registration dialog: asks the user for their SAP username once, maps it to their
/// Teams ID in the database, and returns the SAP username to the caller.
/// We intentionally skip Azure AD SSO (OAuthPrompt) because the bot currently
/// runs in a shared-tenant dev environment where the OAuth connection may not be
/// provisioned. The Teams identity (From.Id / From.Name) is already trusted by
/// the Bot Framework channel, so we only need the SAP side of the mapping.
/// </summary>
public class SsoDialog : ComponentDialog
{
    private readonly ILogger<SsoDialog> _logger;
    private readonly UserMappingService _userMappingService;

    public SsoDialog(ILogger<SsoDialog> logger, UserMappingService userMappingService)
        : base(nameof(SsoDialog))
    {
        _logger = logger;
        _userMappingService = userMappingService;

        AddDialog(new TextPrompt(nameof(TextPrompt)));

        AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
        {
            AskSapUsernameStepAsync,
            SaveMappingStepAsync
        }));

        InitialDialogId = nameof(WaterfallDialog);
    }

    private async Task<DialogTurnResult> AskSapUsernameStepAsync(
        WaterfallStepContext stepContext,
        CancellationToken cancellationToken)
    {
        var teamsId = stepContext.Context.Activity.From.Id;
        var displayName = stepContext.Context.Activity.From.Name ?? "Unknown User";
        _logger.LogInformation("SsoDialog: checking mapping for TeamsId={TeamsId}", teamsId);

        // If the user is already mapped, skip the prompt entirely.
        var existing = await _userMappingService.GetSapUsernameAsync(teamsId, cancellationToken);
        if (!string.IsNullOrEmpty(existing))
        {
            _logger.LogInformation("SsoDialog: user already mapped to SAP user {SapUser}", existing);
            return await stepContext.EndDialogAsync(existing, cancellationToken);
        }

        // First time: ask for SAP username.
        return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
        {
            Prompt = MessageFactory.Text(
                $"👋 Xin chào **{displayName}**!\n\n" +
                "Để sử dụng bot, bạn cần liên kết tài khoản Teams với SAP một lần duy nhất.\n\n" +
                "Vui lòng nhập **SAP Username** của bạn (ví dụ: `LONGTNQ`):"),
            RetryPrompt = MessageFactory.Text("SAP Username không được để trống. Vui lòng nhập lại:")
        }, cancellationToken);
    }

    private async Task<DialogTurnResult> SaveMappingStepAsync(
        WaterfallStepContext stepContext,
        CancellationToken cancellationToken)
    {
        var teamsId = stepContext.Context.Activity.From.Id;
        var displayName = stepContext.Context.Activity.From.Name ?? "Unknown User";
        var sapUsername = (stepContext.Result as string)?.Trim().ToUpperInvariant() ?? string.Empty;

        if (string.IsNullOrEmpty(sapUsername))
        {
            await stepContext.Context.SendActivityAsync(
                "❌ Không thể liên kết tài khoản. Vui lòng thử lại sau.",
                cancellationToken: cancellationToken);
            return await stepContext.EndDialogAsync(null, cancellationToken);
        }

        await _userMappingService.MapUserAsync(teamsId, displayName, sapUsername, cancellationToken);
        _logger.LogInformation("SsoDialog: mapped TeamsId={TeamsId} -> SapUser={SapUser}", teamsId, sapUsername);

        await stepContext.Context.SendActivityAsync(
            $"✅ Đã liên kết thành công! Tài khoản Teams của bạn đã được kết nối với SAP User: **{sapUsername}**.\n\nBây giờ bạn có thể sử dụng đầy đủ tính năng của bot.",
            cancellationToken: cancellationToken);

        return await stepContext.EndDialogAsync(sapUsername, cancellationToken);
    }
}
