using System.Text.RegularExpressions;
using AISO.Bot.Cards.Builders;
using AISO.Bot.Services;
using AISO.SapIntegration;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace AISO.Bot.Dialogs;

/// <summary>
/// Registration dialog: links the Teams user to an admin-assigned SAP User ID
/// (table <c>sap_link_assignments</c>), validates existence in SAP <c>UserRole</c>,
/// then continues. Free-form linking of arbitrary SAP IDs is not allowed.
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

        var email = await TryGetTeamsEmailAsync(stepContext.Context, teamsId, cancellationToken);
        var assignment = await _userMappingService.FindLinkAssignmentAsync(teamsId, email, cancellationToken);
        if (assignment is null)
        {
            var noAssignment =
                "No SAP User ID is assigned to your Teams account yet. " +
                "Ask your admin to add your email in sap_link_assignments, then try again.";
            await stepContext.Context.SendActivityAsync(
                MessageFactory.Attachment(
                    TeamsCardBuilder.BuildLinkSapAccountCard(displayName, noAssignment)),
                cancellationToken);
            return await stepContext.EndDialogAsync(null, cancellationToken);
        }

        stepContext.Values["assignedSapUserId"] = assignment.SapUserId;
        stepContext.Values["teamsEmail"] = email ?? string.Empty;

        var error = stepContext.Options as string;
        return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
        {
            Prompt = (Activity)MessageFactory.Attachment(
                TeamsCardBuilder.BuildLinkSapAccountCard(
                    displayName,
                    error,
                    assignedSapUserId: assignment.SapUserId)),
            RetryPrompt = (Activity)MessageFactory.Attachment(
                TeamsCardBuilder.BuildLinkSapAccountCard(
                    displayName,
                    "SAP User ID cannot be empty. Type your assigned ID to confirm.",
                    assignedSapUserId: assignment.SapUserId))
        }, cancellationToken);
    }

    private async Task<DialogTurnResult> SaveMappingStepAsync(
        WaterfallStepContext stepContext,
        CancellationToken cancellationToken)
    {
        var teamsId = stepContext.Context.Activity.From.Id;
        var displayName = stepContext.Context.Activity.From.Name ?? "Unknown User";
        var sapUsername = (stepContext.Result as string)?.Trim().ToUpperInvariant() ?? string.Empty;
        var email = stepContext.Values.TryGetValue("teamsEmail", out var emailObj)
            ? emailObj as string
            : null;
        if (string.IsNullOrWhiteSpace(email))
            email = await TryGetTeamsEmailAsync(stepContext.Context, teamsId, cancellationToken);

        var validationError = await ValidateSapUserIdAsync(
            teamsId,
            email,
            sapUsername,
            cancellationToken);
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

        var assignment = await _userMappingService.FindLinkAssignmentAsync(teamsId, email, cancellationToken);
        await _userMappingService.MapUserAsync(
            teamsId,
            displayName,
            sapUsername,
            cancellationToken,
            role: assignment?.Role,
            salesOrg: assignment?.SalesOrg,
            delegatedBy: assignment?.DelegatedBySapUser);

        if (assignment is not null)
            await _userMappingService.BindAssignmentTeamsUserAsync(assignment, teamsId, cancellationToken);

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

    private async Task<string?> ValidateSapUserIdAsync(
        string teamsUserId,
        string? teamsEmail,
        string sapUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sapUserId))
            return "SAP User ID cannot be empty. Type your assigned ID to confirm.";

        if (sapUserId.Contains('@', StringComparison.Ordinal)
            || sapUserId.Contains(' ', StringComparison.Ordinal))
        {
            return "That looks like an email or Teams name. Enter your assigned SAP User ID (format: DEV-xxx).";
        }

        if (!SapUserIdPattern.IsMatch(sapUserId))
        {
            return "Invalid SAP User ID format. Use 3–12 characters: letters, digits, hyphen, or underscore (format: DEV-xxx).";
        }

        var assignment = await _userMappingService.FindLinkAssignmentAsync(
            teamsUserId,
            teamsEmail,
            cancellationToken);
        if (assignment is null)
        {
            return "No SAP User ID is assigned to your Teams account yet. Ask your admin to provision sap_link_assignments.";
        }

        if (!string.Equals(assignment.SapUserId, sapUserId, StringComparison.OrdinalIgnoreCase))
        {
            return "That SAP User ID is not assigned to your Teams account. Type the ID your admin assigned (shown on the card).";
        }

        if (await _userMappingService.IsSapUserLinkedToOtherTeamsUserAsync(
                sapUserId, teamsUserId, cancellationToken))
        {
            return $"SAP User ID **{sapUserId}** is already linked to another Teams account. Ask your admin for help.";
        }

        var exists = await _sap.SapUserExistsAsync(sapUserId, cancellationToken);
        if (exists == true)
            return null;

        if (exists == false)
        {
            return $"SAP User ID **{sapUserId}** was not found in AISO (ZAISO_USER_ROLE). Ask your admin to register it in SAP.";
        }

        return "Cannot verify SAP User ID right now: the SAP **UserRole** service is unavailable. Ask the SAP team to expose/publish `ZI_AISO_USER_ROLE` as `UserRole`, then try again.";
    }

    private async Task<string?> TryGetTeamsEmailAsync(
        ITurnContext turnContext,
        string teamsUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            var member = await TeamsInfo.GetMemberAsync(turnContext, teamsUserId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(member?.Email))
                return UserMappingService.NormalizeEmail(member.Email);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SsoDialog: could not resolve Teams email for {TeamsId}", teamsUserId);
        }

        return null;
    }
}
