using System.Text.RegularExpressions;
using AISO.Bot.Cards.Builders;
using AISO.Bot.Services;
using AISO.SapIntegration;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace AISO.Bot.Dialogs;

/// <summary>
/// Registration dialog: asks once for a real SAP User ID, validates format + existence
/// against SAP <c>UserRole</c> / <c>ZAISO_USER_ROLE</c>, maps it to the Teams user, then continues.
/// Azure AD SSO is intentionally skipped in the shared-tenant demo environment.
/// </summary>
public class SsoDialog : ComponentDialog
{
    /// <summary>SAP BNAME-style: letters/digits/underscore/hyphen, 3–12 chars.</summary>
    private static readonly Regex SapUserIdPattern = new(
        @"^[A-Z0-9][A-Z0-9_-]{2,11}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly ILogger<SsoDialog> _logger;
    private readonly UserMappingService _userMappingService;
    private readonly ISapClient _sap;

    public SsoDialog(
        ILogger<SsoDialog> logger,
        UserMappingService userMappingService,
        ISapClient sap)
        : base(nameof(SsoDialog))
    {
        _logger = logger;
        _userMappingService = userMappingService;
        _sap = sap;

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

        var existing = await _userMappingService.GetSapUsernameAsync(teamsId, cancellationToken);
        if (!string.IsNullOrEmpty(existing))
        {
            _logger.LogInformation("SsoDialog: user already mapped to SAP user {SapUser}", existing);
            return await stepContext.EndDialogAsync(existing, cancellationToken);
        }

        var error = stepContext.Options as string;
        return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
        {
            Prompt = (Activity)MessageFactory.Attachment(
                TeamsCardBuilder.BuildLinkSapAccountCard(displayName, error)),
            RetryPrompt = (Activity)MessageFactory.Attachment(
                TeamsCardBuilder.BuildLinkSapAccountCard(
                    displayName,
                    "SAP User ID cannot be empty. Example: DEV-249"))
        }, cancellationToken);
    }

    private async Task<DialogTurnResult> SaveMappingStepAsync(
        WaterfallStepContext stepContext,
        CancellationToken cancellationToken)
    {
        var teamsId = stepContext.Context.Activity.From.Id;
        var displayName = stepContext.Context.Activity.From.Name ?? "Unknown User";
        var sapUsername = (stepContext.Result as string)?.Trim().ToUpperInvariant() ?? string.Empty;

        var validationError = await ValidateSapUserIdAsync(sapUsername, cancellationToken);
        if (validationError is not null)
        {
            _logger.LogWarning(
                "SsoDialog: rejected SAP user {SapUser} for TeamsId={TeamsId}: {Reason}",
                sapUsername,
                teamsId,
                validationError);

            return await stepContext.ReplaceDialogAsync(
                InitialDialogId,
                validationError,
                cancellationToken);
        }

        await _userMappingService.MapUserAsync(teamsId, displayName, sapUsername, cancellationToken);
        _logger.LogInformation("SsoDialog: mapped TeamsId={TeamsId} -> SapUser={SapUser}", teamsId, sapUsername);

        await stepContext.Context.SendActivityAsync(
            MessageFactory.Attachment(
                TeamsCardBuilder.BuildWelcomeCard(displayName)),
            cancellationToken);

        await stepContext.Context.SendActivityAsync(
            $"Linked successfully to SAP User ID **{sapUsername}**. You can start with \"recent orders\" or \"help\".",
            cancellationToken: cancellationToken);

        return await stepContext.EndDialogAsync(sapUsername, cancellationToken);
    }

    private async Task<string?> ValidateSapUserIdAsync(string sapUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sapUserId))
            return "SAP User ID cannot be empty. Example: DEV-249";

        if (sapUserId.Contains('@', StringComparison.Ordinal)
            || sapUserId.Contains(' ', StringComparison.Ordinal))
        {
            return "That looks like an email or Teams name. Enter your SAP User ID (example: DEV-249).";
        }

        if (!SapUserIdPattern.IsMatch(sapUserId))
        {
            return "Invalid SAP User ID format. Use 3–12 characters: letters, digits, hyphen, or underscore (example: DEV-249).";
        }

        var exists = await _sap.SapUserExistsAsync(sapUserId, cancellationToken);
        if (exists == true)
            return null;

        if (exists == false)
        {
            return $"SAP User ID **{sapUserId}** was not found in AISO (ZAISO_USER_ROLE). Ask your admin to register it, or use a seeded ID such as DEV-249.";
        }

        return "Cannot verify SAP User ID right now: the SAP **UserRole** service is unavailable. Ask the SAP team to expose/publish `ZI_AISO_USER_ROLE` as `UserRole`, then try again.";
    }
}
