
using System.Diagnostics;
using AISO.AiOrchestration;
using AISO.Bot.Cards;
using AISO.Bot.Cards.Builders;
using AISO.Bot.Notifications;
using AISO.Domain.Approvals;
using AISO.Domain.SalesOrders;
using AISO.Domain.Users;
using AISO.Persistence.Auditing;
using AISO.SapIntegration;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog.Context;

using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Teams;
using AISO.Bot.Dialogs;
using AISO.Bot.Services;
using Microsoft.Extensions.Configuration;

namespace AISO.Bot;

public class TeamsBot : TeamsActivityHandler
{
    private readonly IFunctionDispatcher _dispatcher;
    private readonly ISapClient _sap;
    private readonly IAuditLogger _audit;
    private readonly IOrderApprovalService _approvals;
    private readonly ILogger<TeamsBot> _logger;
    private readonly ConversationState _conversationState;
    private readonly UserState _userState;
    private readonly SsoDialog _dialog;
    private readonly UserMappingService _userMappingService;
    private readonly IBotUserAdminService _botUserAdmin;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;
    private readonly IUserScopeLookup _scopeLookup;
    private readonly AISO.Domain.Notifications.IEmailService _emailService;

    public TeamsBot(
        IFunctionDispatcher dispatcher,
        ISapClient sap,
        IAuditLogger audit,
        IOrderApprovalService approvals,
        ILogger<TeamsBot> logger,
        ConversationState conversationState,
        UserState userState,
        SsoDialog dialog,
        UserMappingService userMappingService,
        IBotUserAdminService botUserAdmin,
        Microsoft.Extensions.Configuration.IConfiguration config,
        IUserScopeLookup scopeLookup,
        AISO.Domain.Notifications.IEmailService emailService)
    {
        _dispatcher = dispatcher;
        _sap = sap;
        _audit = audit;
        _approvals = approvals;
        _logger = logger;
        _conversationState = conversationState;
        _userState = userState;
        _dialog = dialog;
        _userMappingService = userMappingService;
        _botUserAdmin = botUserAdmin;
        _config = config;
        _scopeLookup = scopeLookup;
        _emailService = emailService;
    }

    public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
    {
        await base.OnTurnAsync(turnContext, cancellationToken);

        // Save any state changes that might have occurred during the turn.
        await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
        await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
    }

    protected override async Task OnMessageActivityAsync(
        ITurnContext<IMessageActivity> turnContext,
        CancellationToken cancellationToken)
    {
        var userMessage = turnContext.Activity.RemoveRecipientMention() ?? turnContext.Activity.Text ?? string.Empty;
        userMessage = System.Text.RegularExpressions.Regex.Replace(userMessage, "<[^>]*>", string.Empty);

        var teamsUserId = turnContext.Activity.From?.Id ?? "anonymous";
        var conversationId = turnContext.Activity.Conversation?.Id;
        var activityId = turnContext.Activity.Id;

        // Prioritize Activity.Value payload over Activity.Text for Adaptive Card submissions.
        if (turnContext.Activity.Value != null)
        {
            try
            {
                var valueObj = Newtonsoft.Json.Linq.JObject.FromObject(turnContext.Activity.Value);

                // Extract msteams.text to handle messageBack payloads reliably.
                if (valueObj.TryGetValue("msteams", StringComparison.OrdinalIgnoreCase, out var msTeamsToken) && msTeamsToken is Newtonsoft.Json.Linq.JObject msTeamsObj)
                {
                    if (msTeamsObj.TryGetValue("text", StringComparison.OrdinalIgnoreCase, out var textToken))
                    {
                        var textVal = textToken.ToString();
                        if (!string.IsNullOrWhiteSpace(textVal))
                        {
                            userMessage = textVal;

                            if (valueObj.TryGetValue("comment", StringComparison.OrdinalIgnoreCase, out var commentToken))
                            {
                                userMessage += $" comment: {commentToken.ToString()}";
                            }
                            if (valueObj.TryGetValue("reasonCode", StringComparison.OrdinalIgnoreCase, out var reasonToken))
                            {
                                userMessage += $" reason: {reasonToken.ToString()}";
                            }
                        }
                    }
                }

                if (valueObj.TryGetValue("command", StringComparison.OrdinalIgnoreCase, out var cmdToken))
                {
                    userMessage = cmdToken.ToString();
                }
                else if (valueObj.TryGetValue("action", StringComparison.OrdinalIgnoreCase, out var actionToken))
                {
                    var action = actionToken.ToString();
                    if (string.Equals(action, "view_details", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken)
                            ? idToken.ToString()
                            : "UNKNOWN";

                        try
                        {
                            var order = await _sap.GetSalesOrderByIdAsync(salesOrderId, cancellationToken);
                            if (order is null)
                            {
                                await turnContext.SendActivityAsync(
                                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                        "NOT_FOUND",
                                        $"Sales order {salesOrderId} was not found.")),
                                    cancellationToken);
                                return;
                            }

                            var roleForDetail = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                            var linkedSapForDetail = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(await BuildSalesOrderDetailAttachmentAsync(
                                    order,
                                    roleForDetail,
                                    linkedSapForDetail,
                                    cancellationToken)),
                                cancellationToken);
                        }
                        catch (SapODataException sapEx)
                        {
                            _logger.LogError(sapEx, "SAP error viewing order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("SAP_ERROR", sapEx.Message)),
                                cancellationToken: cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error viewing order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("ACTION_FAILED", ex.Message)),
                                cancellationToken: cancellationToken);
                        }
                        return;
                    }

                    if (string.Equals(action, "release_so", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken) ? idToken.ToString() : "UNKNOWN";
                        var roleForConfirm = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                        var linkedSapForGate = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        // Employees request release — soft-lock if already pending. Managers may still open release/approve.
                        var blockIfPending = roleForConfirm == UserRole.Employee;
                        if (!await EnsureLifecycleActionAllowedAsync(
                                turnContext,
                                salesOrderId,
                                roleForConfirm == UserRole.Employee ? "Request release" : "Release",
                                cancellationToken,
                                blockIfPendingApproval: blockIfPending,
                                blockIfNotOwner: roleForConfirm == UserRole.Employee,
                                currentSapUser: linkedSapForGate))
                        {
                            return;
                        }

                        var confirmCard = roleForConfirm == UserRole.Employee
                            ? TeamsCardBuilder.BuildConfirmRequestReleaseCard(salesOrderId)
                            : TeamsCardBuilder.BuildConfirmReleaseCard(salesOrderId);
                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(confirmCard),
                            cancellationToken);
                        return;
                    }

                    if (string.Equals(action, "approve_so", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken) ? idToken.ToString() : "UNKNOWN";
                        if (!await EnsureLifecycleActionAllowedAsync(turnContext, salesOrderId, "Approve / release", cancellationToken))
                        {
                            return;
                        }

                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildConfirmApproveCard(salesOrderId)),
                            cancellationToken);
                        return;
                    }

                    if (string.Equals(action, "reject_approval", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken) ? idToken.ToString() : "UNKNOWN";
                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildConfirmRejectApprovalCard(salesOrderId)),
                            cancellationToken);
                        return;
                    }

                    if (string.Equals(action, "filter_pending_approvals", StringComparison.OrdinalIgnoreCase))
                    {
                        var role = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                        if (role is not (UserRole.Manager or UserRole.Admin))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildNotAuthorizedCard(
                                    "Only managers and administrators can view pending approvals.",
                                    role.ToString(),
                                    "Manager")),
                                cancellationToken);
                            return;
                        }

                        var salesOrgScope = role == UserRole.Admin
                            ? null
                            : await _userMappingService.GetSalesOrgAsync(teamsUserId, cancellationToken);
                        var pending = await _approvals.GetPendingAsync(salesOrgScope, cancellationToken);
                        var search = valueObj.Value<string>("search");
                        var requester = valueObj.Value<string>("requester");

                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildPendingApprovalsCard(
                                pending,
                                search,
                                requester)),
                            cancellationToken);
                        return;
                    }

                    if (string.Equals(action, "manage_bot_user", StringComparison.OrdinalIgnoreCase))
                    {
                        var role = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                        if (role != UserRole.Admin)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildNotAuthorizedCard(
                                    "Only administrators can manage bot users.",
                                    role.ToString(),
                                    "Admin")),
                                cancellationToken);
                            return;
                        }

                        var sapUserId = valueObj.TryGetValue("sapUserId", StringComparison.OrdinalIgnoreCase, out var sapToken)
                            ? sapToken.ToString()
                            : null;
                        if (string.IsNullOrWhiteSpace(sapUserId))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "VALIDATION",
                                    "Missing SAP user id.")),
                                cancellationToken);
                            return;
                        }

                        var user = await _botUserAdmin.GetBySapUserIdAsync(sapUserId, cancellationToken);
                        if (user is null)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "NOT_FOUND",
                                    $"No linked Teams user found for SAP ID {sapUserId}.")),
                                cancellationToken);
                            return;
                        }

                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildManageBotUserCard(user)),
                            cancellationToken);
                        return;
                    }

                    if (string.Equals(action, "manage_bot_user_confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        var role = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                        if (role != UserRole.Admin)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildNotAuthorizedCard(
                                    "Only administrators can manage bot users.",
                                    role.ToString(),
                                    "Admin")),
                                cancellationToken);
                            return;
                        }

                        var sapUserId = valueObj.TryGetValue("sapUserId", StringComparison.OrdinalIgnoreCase, out var sapToken)
                            ? sapToken.ToString()
                            : null;
                        var newRoleRaw = valueObj.Value<string>("role");
                        var newSalesOrg = valueObj.Value<string>("salesOrg");

                        if (string.IsNullOrWhiteSpace(sapUserId)
                            || !Enum.TryParse<UserRole>(newRoleRaw, ignoreCase: true, out var newRole))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "VALIDATION",
                                    "Role is required (Employee, Manager, or Admin).")),
                                cancellationToken);
                            return;
                        }

                        try
                        {
                            // Capture prior values for the change-notification email before we mutate.
                            var oldRole = await _scopeLookup.GetRoleBySapUserAsync(sapUserId, cancellationToken);
                            var oldSalesOrg = await _scopeLookup.GetSalesOrgBySapUserAsync(sapUserId, cancellationToken);

                            var updated = await _botUserAdmin.UpdateAccessAsync(
                                sapUserId,
                                newRole,
                                newSalesOrg,
                                cancellationToken);

                            var adminSap = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                            string? sapSyncWarning = null;
                            if (string.IsNullOrWhiteSpace(adminSap))
                            {
                                sapSyncWarning = "Bot access updated, but SAP role was not synced (Admin has no linked SAP User ID).";
                            }
                            else
                            {
                                try
                                {
                                    await _sap.SyncUserRoleAsync(
                                        updated.SapUserId,
                                        updated.Role.ToString().ToUpperInvariant(),
                                        updated.SalesOrg,
                                        adminSap,
                                        cancellationToken);
                                }
                                catch (Exception syncEx)
                                {
                                    _logger.LogWarning(
                                        syncEx,
                                        "SAP syncUserRole failed for {SapUser} after bot UpdateAccess",
                                        updated.SapUserId);
                                    sapSyncWarning =
                                        $"Bot access updated, but SAP syncUserRole failed: {syncEx.Message}";
                                }
                            }

                            await _audit.LogAsync(new AuditEntry
                            {
                                TeamsUserId = teamsUserId,
                                ConversationId = conversationId,
                                Action = "ManageBotUser",
                                ParametersJson = JsonConvert.SerializeObject(new
                                {
                                    sap_user_id = updated.SapUserId,
                                    role = updated.Role.ToString(),
                                    sales_org = updated.SalesOrg,
                                    sap_sync = sapSyncWarning is null ? "ok" : "failed"
                                }),
                                ResultStatus = sapSyncWarning is null ? "Success" : "PartialSuccess"
                            }, cancellationToken);

                            if (sapSyncWarning is not null)
                            {
                                await turnContext.SendActivityAsync(
                                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                        "SAP_SYNC",
                                        sapSyncWarning)),
                                    cancellationToken);
                                return;
                            }

                            // Notify the affected user via email — best effort, mutation already succeeded.
                            // Skip when nothing actually changed (no role + no sales-org delta).
                            if (UserAccessChangeEmailBuilder.HasChange(oldRole, updated.Role, oldSalesOrg, updated.SalesOrg))
                            {
                                try
                                {
                                    var targetEmail = await _scopeLookup.GetEmailBySapUserAsync(updated.SapUserId, cancellationToken);
                                    if (!string.IsNullOrWhiteSpace(targetEmail))
                                    {
                                        var subject = _config["Email:SalesOrgChangeSubject"]
                                            ?? "Your AISO access has been updated";
                                        var html = UserAccessChangeEmailBuilder.Build(
                                            displayName: string.IsNullOrWhiteSpace(updated.DisplayName) ? updated.SapUserId : updated.DisplayName,
                                            adminSapUser: adminSap ?? "an administrator",
                                            oldRole: oldRole,
                                            newRole: updated.Role,
                                            oldSalesOrg: oldSalesOrg,
                                            newSalesOrg: updated.SalesOrg);
                                        await _emailService.SendEmailAsync(targetEmail, subject, html, cancellationToken);
                                    }
                                    else
                                    {
                                        _logger.LogInformation(
                                            "Skipped access-change email for {SapUser}: no Teams email on file",
                                            updated.SapUserId);
                                    }
                                }
                                catch (Exception emailEx)
                                {
                                    _logger.LogWarning(
                                        emailEx,
                                        "Failed to send access-change email for {SapUser}",
                                        updated.SapUserId);
                                }
                            }

                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildSuccessCard(
                                    updated.SapUserId,
                                    "UserAccessUpdated",
                                    $"{updated.Role}" + (string.IsNullOrWhiteSpace(updated.SalesOrg)
                                        ? ""
                                        : $" / {updated.SalesOrg}"))),
                                cancellationToken);
                        }
                        catch (InvalidOperationException invEx)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "VALIDATION",
                                    invEx.Message)),
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to update bot user {SapUserId}", sapUserId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "ACTION_FAILED",
                                    ex.Message)),
                                cancellationToken);
                        }

                        return;
                    }

                    if (string.Equals(action, "reject_so", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken) ? idToken.ToString() : "UNKNOWN";
                        var linkedSapForGate = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (!await EnsureLifecycleActionAllowedAsync(
                                turnContext,
                                salesOrderId,
                                "Reject",
                                cancellationToken,
                                blockIfPendingApproval: true,
                                blockIfNotOwner: true,
                                currentSapUser: linkedSapForGate))
                        {
                            return;
                        }

                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildConfirmRejectCard(salesOrderId)),
                            cancellationToken);
                        return;
                    }

                    if (string.Equals(action, "cancel_so", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken)
                            ? idToken.ToString()
                            : "UNKNOWN";
                        var roleForCancel = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                        if (roleForCancel < UserRole.Manager)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildNotAuthorizedCard(
                                    "Only Manager or Admin can cancel from the order detail card. Employees can type \"cancel order {id}\" for their own orders, or use Reject order.",
                                    roleForCancel.ToString(),
                                    UserRole.Manager.ToString())),
                                cancellationToken);
                            return;
                        }

                        if (!await EnsureLifecycleActionAllowedAsync(
                                turnContext,
                                salesOrderId,
                                "Cancel",
                                cancellationToken,
                                blockIfPendingApproval: false,
                                blockIfNotOwner: false))
                        {
                            return;
                        }

                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildConfirmCancelCard(salesOrderId)),
                            cancellationToken);
                        return;
                    }

                    if (string.Equals(action, "update_ref", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken)
                            ? idToken.ToString()
                            : "UNKNOWN";
                        var linkedSapForGate = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (!await EnsureLifecycleActionAllowedAsync(
                                turnContext,
                                salesOrderId,
                                "Update reference",
                                cancellationToken,
                                blockIfPendingApproval: true,
                                blockIfNotOwner: true,
                                currentSapUser: linkedSapForGate))
                        {
                            return;
                        }

                        var order = await _sap.GetSalesOrderByIdAsync(salesOrderId, cancellationToken);
                        var currentRef = order?.CustomerReference ?? string.Empty;
                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildConfirmUpdateReferenceCard(
                                order?.SoNumber ?? salesOrderId,
                                currentRef,
                                currentRef)),
                            cancellationToken);
                        return;
                    }

                    if (string.Equals(action, "edit_so", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken)
                            ? idToken.ToString()
                            : "UNKNOWN";
                        var linkedSapForGate = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        var roleForEdit = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                        if (!await EnsureLifecycleActionAllowedAsync(
                                turnContext,
                                salesOrderId,
                                "Edit",
                                cancellationToken,
                                blockIfPendingApproval: roleForEdit < UserRole.Manager,
                                blockIfNotOwner: roleForEdit < UserRole.Manager,
                                currentSapUser: linkedSapForGate))
                        {
                            return;
                        }

                        if (roleForEdit == UserRole.Manager)
                        {
                            var orderScope = await _sap.GetSalesOrderByIdAsync(salesOrderId, cancellationToken);
                            var managerSalesOrg = await _userMappingService.GetSalesOrgAsync(teamsUserId, cancellationToken);
                            if (orderScope is not null
                                && !string.IsNullOrWhiteSpace(managerSalesOrg)
                                && !string.IsNullOrWhiteSpace(orderScope.SalesOrg)
                                && !string.Equals(managerSalesOrg, orderScope.SalesOrg, StringComparison.OrdinalIgnoreCase))
                            {
                                await turnContext.SendActivityAsync(
                                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                        "VALIDATION",
                                        $"Order {orderScope.SoNumber} belongs to sales org {orderScope.SalesOrg}; your scope is {managerSalesOrg}.")),
                                    cancellationToken);
                                return;
                            }
                        }

                        var order = await _sap.GetSalesOrderByIdAsync(salesOrderId, cancellationToken);
                        if (order is null)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "NOT_FOUND",
                                    $"Sales order {salesOrderId} was not found.")),
                                cancellationToken);
                            return;
                        }

                        var first = order.Items?.FirstOrDefault();
                        var linesSummary = order.Items is { Count: > 0 }
                            ? string.Join("; ", order.Items.Select(i =>
                                $"{(string.IsNullOrWhiteSpace(i.ItemNumber) ? "—" : i.ItemNumber.TrimStart('0'))} · {i.Material} x {i.Quantity:0} {i.Unit}"))
                            : "No line items";

                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildConfirmEditOrderCard(
                                order.SoNumber,
                                order.CustomerReference ?? string.Empty,
                                order.CustomerReference ?? string.Empty,
                                order.RequestedDeliveryDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                                order.RequestedDeliveryDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                                "none",
                                first?.ItemNumber?.TrimStart('0') ?? "10",
                                first?.Material ?? string.Empty,
                                first?.Quantity ?? 1m,
                                "1010",
                                first?.Unit ?? "PC",
                                linesSummary)),
                            cancellationToken);
                        return;
                    }

                    if (string.Equals(action, "forward_so", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken) ? idToken.ToString() : "UNKNOWN";
                        var linkedSapForGate = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (!await EnsureLifecycleActionAllowedAsync(
                                turnContext,
                                salesOrderId,
                                "Forward",
                                cancellationToken,
                                blockIfPendingApproval: true,
                                blockIfNotOwner: true,
                                currentSapUser: linkedSapForGate))
                        {
                            return;
                        }

                        var orderForRecipients = await _sap.GetSalesOrderByIdAsync(salesOrderId, cancellationToken);
                        var recipientChoices = await _userMappingService.GetForwardRecipientChoicesAsync(
                            cancellationToken,
                            excludeSapUserId: linkedSapForGate,
                            salesOrgFromOrder: orderForRecipients?.SalesOrg);

                        var senderDisplayName = await _userMappingService.GetDisplayNameAsync(teamsUserId, cancellationToken);
                        var senderSapUsername = linkedSapForGate;
                        var senderName = !string.IsNullOrWhiteSpace(senderDisplayName)
                            ? senderDisplayName
                            : turnContext.Activity.From?.Name ?? "Unknown user";

                        if (!string.IsNullOrWhiteSpace(senderSapUsername)
                            && !string.Equals(senderName, senderSapUsername, StringComparison.OrdinalIgnoreCase))
                        {
                            senderName = $"{senderName} ({senderSapUsername})";
                        }

                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildConfirmForwardCard(salesOrderId, recipientChoices, senderName)),
                            cancellationToken);
                        return;
                    }

                    if (string.Equals(action, "release_so_confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken) ? idToken.ToString() : "UNKNOWN";
                        var comment = valueObj.TryGetValue("comment", StringComparison.OrdinalIgnoreCase, out var commentToken)
                            ? commentToken.ToString()
                            : null;

                        var linkedSapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (string.IsNullOrWhiteSpace(linkedSapUsername))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "NOT_LINKED",
                                    "No SAP account is linked to your Teams identity yet.")),
                                cancellationToken);
                            return;
                        }

                        var role = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);

                        try
                        {
                            // Maker-checker: Employees submit a request; Managers/Admins release (or approve pending).
                            if (role == UserRole.Employee)
                            {
                                var order = await _sap.GetSalesOrderByIdAsync(salesOrderId, cancellationToken);
                                if (order is null)
                                {
                                    await turnContext.SendActivityAsync(
                                        MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("NOT_FOUND", $"Sales order {salesOrderId} was not found.")),
                                        cancellationToken);
                                    return;
                                }

                                if (SalesOrderWorkflow.BlocksReleaseRejectForward(order.Status))
                                {
                                    await turnContext.SendActivityAsync(
                                        MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                            "VALIDATION",
                                            SalesOrderWorkflow.BuildBlockedMessage(order.Status, "Request release"))),
                                        cancellationToken);
                                    return;
                                }

                                if (order.HasInvalidMaterial)
                                {
                                    await turnContext.SendActivityAsync(
                                        MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                            "VALIDATION",
                                            SalesOrderWorkflow.BuildInvalidMaterialBlockedMessage("Request release"))),
                                        cancellationToken);
                                    return;
                                }

                                var existingPending = await _approvals.GetPendingBySoNumberAsync(order.SoNumber, cancellationToken);
                                if (existingPending is not null)
                                {
                                    await turnContext.SendActivityAsync(
                                        MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                            "VALIDATION",
                                            SalesOrderWorkflow.BuildPendingApprovalBlockedMessage(
                                                "Request release",
                                                existingPending.RequestedBySapUser))),
                                        cancellationToken);
                                    return;
                                }

                                var request = await _approvals.RequestReleaseAsync(
                                    order.SoNumber,
                                    linkedSapUsername,
                                    order.SalesOrg,
                                    comment,
                                    cancellationToken);

                                await turnContext.SendActivityAsync(
                                    MessageFactory.Attachment(
                                        TeamsCardBuilder.BuildSuccessCard(request.SoNumber, "ReleaseRequested")),
                                    cancellationToken);
                                return;
                            }

                            if (!await EnsureLifecycleActionAllowedAsync(turnContext, salesOrderId, "Release", cancellationToken))
                            {
                                return;
                            }

                            var pending = await _approvals.GetPendingBySoNumberAsync(salesOrderId, cancellationToken);
                            if (pending is not null)
                            {
                                var managerSalesOrg = await _userMappingService.GetSalesOrgAsync(teamsUserId, cancellationToken);
                                var isAdmin = role == UserRole.Admin;
                                if (!isAdmin
                                    && !string.IsNullOrWhiteSpace(managerSalesOrg)
                                    && !string.IsNullOrWhiteSpace(pending.SalesOrg)
                                    && !string.Equals(pending.SalesOrg, managerSalesOrg, StringComparison.OrdinalIgnoreCase))
                                {
                                    await turnContext.SendActivityAsync(
                                        MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                            "NOT_AUTHORIZED",
                                            $"Order {pending.SoNumber} belongs to sales org {pending.SalesOrg}; your scope is {managerSalesOrg}.")),
                                        cancellationToken);
                                    return;
                                }

                                // SAP: only approveOrder buffers real RELEASE (clear LIFSK); releaseOrder is audit-only.
                                var updated = await _sap.ApproveOrderAsync(pending.SoNumber, linkedSapUsername, cancellationToken);
                                await _approvals.ApproveAsync(
                                    pending.SoNumber,
                                    linkedSapUsername,
                                    managerSalesOrg,
                                    isAdmin,
                                    comment,
                                    cancellationToken);

                                await turnContext.SendActivityAsync(
                                    MessageFactory.Attachment(TeamsCardBuilder.BuildSuccessCard(updated.SoNumber, "Approved")),
                                    cancellationToken);
                                return;
                            }

                            // Manager direct release (no pending): still must call approveOrder to clear delivery block.
                            var updatedOrder = await _sap.ApproveOrderAsync(salesOrderId, linkedSapUsername, cancellationToken);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildSuccessCard(updatedOrder.SoNumber, "Released")),
                                cancellationToken);
                        }
                        catch (UnauthorizedAccessException authEx)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("NOT_AUTHORIZED", authEx.Message)),
                                cancellationToken: cancellationToken);
                        }
                        catch (InvalidOperationException invEx)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("VALIDATION", invEx.Message)),
                                cancellationToken: cancellationToken);
                        }
                        catch (SapODataException sapEx)
                        {
                            _logger.LogError(sapEx, "SAP error releasing order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("SAP_ERROR", sapEx.Message)),
                                cancellationToken: cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error releasing order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("ACTION_FAILED", ex.Message)),
                                cancellationToken: cancellationToken);
                        }
                        return;
                    }

                    if (string.Equals(action, "delegate_confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        var delegateUser = valueObj.TryGetValue("delegateUser", StringComparison.OrdinalIgnoreCase, out var uToken) ? uToken.ToString() : null;
                        var validFromStr = valueObj.TryGetValue("validFromRaw", StringComparison.OrdinalIgnoreCase, out var vfToken) ? vfToken.ToString() : null;
                        if (string.IsNullOrWhiteSpace(validFromStr))
                        {
                            validFromStr = valueObj.TryGetValue("validFrom", StringComparison.OrdinalIgnoreCase, out var vfToken2) ? vfToken2.ToString() : null;
                        }

                        var validToStr = valueObj.TryGetValue("validToRaw", StringComparison.OrdinalIgnoreCase, out var vtToken) ? vtToken.ToString() : null;
                        if (string.IsNullOrWhiteSpace(validToStr))
                        {
                            validToStr = valueObj.TryGetValue("validTo", StringComparison.OrdinalIgnoreCase, out var vtToken2) ? vtToken2.ToString() : null;
                        }

                        var maxAmountStr = valueObj.TryGetValue("maxAmountRaw", StringComparison.OrdinalIgnoreCase, out var maToken) ? maToken.ToString() : null;
                        if (string.IsNullOrWhiteSpace(maxAmountStr))
                        {
                            maxAmountStr = valueObj.TryGetValue("maxAmount", StringComparison.OrdinalIgnoreCase, out var maToken2) ? maToken2.ToString() : null;
                        }

                        var currency = valueObj.TryGetValue("currency", StringComparison.OrdinalIgnoreCase, out var currToken) ? currToken.ToString() : "VND";
                        var reason = valueObj.TryGetValue("reason", StringComparison.OrdinalIgnoreCase, out var rToken) ? rToken.ToString() : null;

                        var linkedSapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (string.IsNullOrWhiteSpace(linkedSapUsername))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("NOT_LINKED", "No SAP account is linked to your Teams identity yet.")),
                                cancellationToken);
                            return;
                        }

                        if (string.IsNullOrWhiteSpace(delegateUser) || string.IsNullOrWhiteSpace(validFromStr) || string.IsNullOrWhiteSpace(validToStr))
                        {
                            await turnContext.SendActivityAsync(MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("VALIDATION", "Delegate user or dates are missing.")), cancellationToken);
                            return;
                        }

                        var fromDate = DateTimeOffset.Parse(validFromStr);
                        var toDate = DateTimeOffset.Parse(validToStr);
                        decimal? maxAmount = null;
                        if (!string.IsNullOrWhiteSpace(maxAmountStr) && decimal.TryParse(maxAmountStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var m))
                        {
                            maxAmount = m;
                        }

                        var salesOrg = await _scopeLookup.GetSalesOrgBySapUserAsync(linkedSapUsername, cancellationToken);

                        var dto = new DelegateApprovalDto(
                            RequestingTeamsUser: linkedSapUsername,
                            DelegateUser: delegateUser,
                            SalesOrg: salesOrg,
                            ValidFrom: fromDate,
                            ValidTo: toDate,
                            Reason: reason,
                            MaxAmount: maxAmount,
                            Currency: currency);

                        try
                        {
                            await _sap.DelegateApprovalAsync(dto, cancellationToken);

                            // Update local DB
                            await _scopeLookup.SetDelegatedBySapUserAsync(delegateUser, linkedSapUsername, toDate, maxAmount, cancellationToken);

                            // Send email notification
                            var delegateEmail = await _scopeLookup.GetEmailBySapUserAsync(delegateUser, cancellationToken);
                            if (!string.IsNullOrEmpty(delegateEmail))
                            {
                                string subject = $"Delegation Notice from {linkedSapUsername}";
                                string html = $@"
                                    <h2>Delegation Notice</h2>
                                    <p>You have been delegated by <b>{linkedSapUsername}</b> to approve SAP orders (Sales Org: {salesOrg ?? "All"}).</p>
                                    <ul>
                                        <li><b>Start Date:</b> {fromDate:dd/MM/yyyy}</li>
                                        <li><b>End Date:</b> {toDate:dd/MM/yyyy}</li>
                                        <li><b>Max Amount:</b> {(maxAmount.HasValue ? $"{maxAmount.Value:N0} {currency}" : "Unlimited")}</li>
                                        <li><b>Reason:</b> {reason ?? "None"}</li>
                                    </ul>
                                    <p>Please log in to the AISO Teams Bot to process approval requests during this period.</p>
                                ";
                                await _emailService.SendEmailAsync(delegateEmail, subject, html, cancellationToken);
                            }

                            string successMsg = $"Successfully delegated to {delegateUser} from {fromDate:dd/MM/yyyy} to {toDate:dd/MM/yyyy}." + (maxAmount.HasValue ? $" Max Amount: {maxAmount.Value:N0} {currency}" : "");
                            await turnContext.SendActivityAsync(MessageFactory.Text(successMsg), cancellationToken);
                        }
                        catch (SapODataException sapEx)
                        {
                            _logger.LogError(sapEx, "SAP error delegating approval for {DelegateUser}", delegateUser);

                            // If SAP says delegation already exists, sync local DB
                            if (sapEx.Message.Contains("already has active delegation", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    await _scopeLookup.SetDelegatedBySapUserAsync(delegateUser, linkedSapUsername, toDate, maxAmount, cancellationToken);
                                    _logger.LogInformation("Synced existing SAP delegation for {DelegateUser} to local DB", delegateUser);
                                }
                                catch (Exception syncEx)
                                {
                                    _logger.LogWarning(syncEx, "Failed to sync SAP delegation for {DelegateUser}", delegateUser);
                                }
                            }

                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("SAP_ERROR", sapEx.Message)),
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error delegating approval for {DelegateUser}", delegateUser);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("ACTION_FAILED", ex.Message)),
                                cancellationToken);
                        }
                        return;
                    }

                    if (string.Equals(action, "revoke_delegation_confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        var delegateUser = valueObj.TryGetValue("delegateUser", StringComparison.OrdinalIgnoreCase, out var uToken) ? uToken.ToString() : null;
                        var delegationId = valueObj.TryGetValue("delegationId", StringComparison.OrdinalIgnoreCase, out var idToken) ? idToken.ToString() : null;

                        var linkedSapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (string.IsNullOrWhiteSpace(linkedSapUsername))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("NOT_LINKED", "No SAP account is linked to your Teams identity yet.")),
                                cancellationToken);
                            return;
                        }

                        if (string.IsNullOrWhiteSpace(delegateUser))
                        {
                            await turnContext.SendActivityAsync(MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("VALIDATION", "Delegate user is missing.")), cancellationToken);
                            return;
                        }

                        var dto = new RevokeDelegationDto(
                            RequestingTeamsUser: linkedSapUsername,
                            DelegateUser: delegateUser);

                        try
                        {
                            await _sap.RevokeDelegationAsync(dto, cancellationToken);

                            // Update local DB
                            await _scopeLookup.SetDelegatedBySapUserAsync(delegateUser, null, null, null, cancellationToken);

                            // Send email notification
                            var delegateEmail = await _scopeLookup.GetEmailBySapUserAsync(delegateUser, cancellationToken);
                            if (!string.IsNullOrEmpty(delegateEmail))
                            {
                                string subject = $"Delegation Revoked by {linkedSapUsername}";
                                string html = $@"
                                    <h2>Delegation Revoked</h2>
                                    <p>Your approval delegation from <b>{linkedSapUsername}</b> has been revoked early.</p>
                                    <p>You can no longer approve SAP orders on their behalf.</p>
                                ";
                                await _emailService.SendEmailAsync(delegateEmail, subject, html, cancellationToken);
                            }

                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildSuccessCard("Delegation", "Revoked", delegateUser)),
                                cancellationToken);
                        }
                        catch (SapODataException sapEx)
                        {
                            _logger.LogError(sapEx, "SAP error revoking delegation for {DelegateUser}", delegateUser);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("SAP_ERROR", sapEx.Message)),
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error revoking delegation for {DelegateUser}", delegateUser);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("ACTION_FAILED", ex.Message)),
                                cancellationToken);
                        }
                        return;
                    }

                    if (string.Equals(action, "request_release_confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken) ? idToken.ToString() : "UNKNOWN";
                        var comment = valueObj.TryGetValue("comment", StringComparison.OrdinalIgnoreCase, out var commentToken)
                            ? commentToken.ToString()
                            : null;

                        var linkedSapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (string.IsNullOrWhiteSpace(linkedSapUsername))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "NOT_LINKED",
                                    "No SAP account is linked to your Teams identity yet.")),
                                cancellationToken);
                            return;
                        }

                        try
                        {
                            var order = await _sap.GetSalesOrderByIdAsync(salesOrderId, cancellationToken);
                            if (order is null)
                            {
                                await turnContext.SendActivityAsync(
                                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("NOT_FOUND", $"Sales order {salesOrderId} was not found.")),
                                    cancellationToken);
                                return;
                            }

                            if (SalesOrderWorkflow.BlocksReleaseRejectForward(order.Status))
                            {
                                await turnContext.SendActivityAsync(
                                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                        "VALIDATION",
                                        SalesOrderWorkflow.BuildBlockedMessage(order.Status, "Request release"))),
                                    cancellationToken);
                                return;
                            }

                            if (order.HasInvalidMaterial)
                            {
                                await turnContext.SendActivityAsync(
                                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                        "VALIDATION",
                                        SalesOrderWorkflow.BuildInvalidMaterialBlockedMessage("Request release"))),
                                    cancellationToken);
                                return;
                            }

                            var existingPending = await _approvals.GetPendingBySoNumberAsync(order.SoNumber, cancellationToken);
                            if (existingPending is not null)
                            {
                                await turnContext.SendActivityAsync(
                                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                        "VALIDATION",
                                        SalesOrderWorkflow.BuildPendingApprovalBlockedMessage(
                                            "Request release",
                                            existingPending.RequestedBySapUser))),
                                    cancellationToken);
                                return;
                            }

                            var request = await _approvals.RequestReleaseAsync(
                                order.SoNumber,
                                linkedSapUsername,
                                order.SalesOrg,
                                comment,
                                cancellationToken);

                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(
                                    TeamsCardBuilder.BuildSuccessCard(request.SoNumber, "ReleaseRequested")),
                                cancellationToken);
                        }
                        catch (InvalidOperationException invEx)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("VALIDATION", invEx.Message)),
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error requesting release for {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("ACTION_FAILED", ex.Message)),
                                cancellationToken);
                        }
                        return;
                    }

                    if (string.Equals(action, "bulk_approve_so", StringComparison.OrdinalIgnoreCase))
                    {
                        var comment = valueObj.TryGetValue("bulk_comment", StringComparison.OrdinalIgnoreCase, out var commentToken)
                            ? commentToken.ToString()
                            : null;

                        var orderIds = new List<string>();
                        foreach (var prop in valueObj.Properties())
                        {
                            if (prop.Name.StartsWith("toggle_", StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(prop.Value.ToString(), "true", StringComparison.OrdinalIgnoreCase))
                            {
                                orderIds.Add(prop.Name.Substring("toggle_".Length));
                            }
                        }

                        if (orderIds.Count == 0)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "VALIDATION",
                                    "Please select at least one order to approve.")),
                                cancellationToken);
                            return;
                        }

                        var linkedSapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (string.IsNullOrWhiteSpace(linkedSapUsername))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "NOT_LINKED",
                                    "No SAP account is linked to your Teams identity yet.")),
                                cancellationToken);
                            return;
                        }

                        var role = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                        var delegatedBy = await _userMappingService.GetDelegatedBySapUserAsync(teamsUserId, cancellationToken);

                        if (role < UserRole.Manager && string.IsNullOrWhiteSpace(delegatedBy))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildNotAuthorizedCard(
                                    "Only Manager or Admin can approve release requests (or a delegated user).",
                                    role.ToString(),
                                    UserRole.Manager.ToString())),
                                cancellationToken);
                            return;
                        }

                        var managerSalesOrg = await _userMappingService.GetSalesOrgAsync(teamsUserId, cancellationToken);
                        var isAdmin = role == UserRole.Admin;

                        int successes = 0;
                        var failures = new List<string>();

                        foreach (var orderId in orderIds)
                        {
                            try
                            {
                                if (!await EnsureLifecycleActionAllowedAsync(turnContext, orderId, "Approve / release", cancellationToken, false))
                                {
                                    failures.Add($"{orderId}: Action not allowed or order is locked.");
                                    continue;
                                }

                                var pending = await _approvals.GetPendingBySoNumberAsync(orderId, cancellationToken);
                                if (pending is null)
                                {
                                    failures.Add($"{orderId}: No pending request found.");
                                    continue;
                                }

                                if (!isAdmin
                                    && !string.IsNullOrWhiteSpace(managerSalesOrg)
                                    && !string.IsNullOrWhiteSpace(pending.SalesOrg)
                                    && !string.Equals(pending.SalesOrg, managerSalesOrg, StringComparison.OrdinalIgnoreCase))
                                {
                                    failures.Add($"{orderId}: Scope mismatch.");
                                    continue;
                                }

                                var existing = await _sap.GetSalesOrderByIdAsync(pending.SoNumber, cancellationToken);
                                if (!isAdmin && existing is not null)
                                {
                                    var thresholdError = AISO.Domain.Approvals.ApprovalThresholdHelper.CheckThreshold(_config, existing.NetValue, existing.Currency);
                                    if (thresholdError is not null)
                                    {
                                        failures.Add($"{orderId}: {thresholdError}");
                                        continue;
                                    }
                                }

                                await _sap.ApproveOrderAsync(pending.SoNumber, linkedSapUsername, cancellationToken);
                                await _approvals.ApproveAsync(pending.SoNumber, linkedSapUsername, managerSalesOrg, isAdmin, comment, cancellationToken);

                                await _audit.LogAsync(new AuditEntry
                                {
                                    TeamsUserId = teamsUserId,
                                    Action = "ApproveOrder",
                                    ParametersJson = $"{{\"orderId\": \"{orderId}\"}}"
                                }, cancellationToken);
                                successes++;
                            }
                            catch (SapODataException sapEx)
                            {
                                failures.Add($"{orderId}: {sapEx.Message}");
                            }
                            catch (Exception ex)
                            {
                                failures.Add($"{orderId}: {ex.Message}");
                            }
                        }

                        var summary = new System.Text.StringBuilder($"**Successfully approved: {successes}**\n\n");
                        if (failures.Count > 0)
                        {
                            summary.AppendLine($"**Failed: {failures.Count}**");
                            foreach (var fail in failures)
                            {
                                summary.AppendLine($"- {fail}");
                            }
                        }

                        var summaryActivity = MessageFactory.Text(summary.ToString());
                        await turnContext.SendActivityAsync(summaryActivity, cancellationToken);

                        // Refresh pending approvals list
                        var updatedPending = await _approvals.GetPendingAsync(isAdmin ? null : managerSalesOrg, cancellationToken);
                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildPendingApprovalsCard(updatedPending, null, null)),
                            cancellationToken);

                        return;
                    }

                    if (string.Equals(action, "approve_so_confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken) ? idToken.ToString() : "UNKNOWN";
                        var comment = valueObj.TryGetValue("comment", StringComparison.OrdinalIgnoreCase, out var commentToken)
                            ? commentToken.ToString()
                            : null;

                        var linkedSapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (string.IsNullOrWhiteSpace(linkedSapUsername))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "NOT_LINKED",
                                    "No SAP account is linked to your Teams identity yet.")),
                                cancellationToken);
                            return;
                        }

                        var role = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                        if (role < UserRole.Manager)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildNotAuthorizedCard(
                                    "Only Manager or Admin can approve release requests.",
                                    role.ToString(),
                                    UserRole.Manager.ToString())),
                                cancellationToken);
                            return;
                        }

                        try
                        {
                            if (!await EnsureLifecycleActionAllowedAsync(turnContext, salesOrderId, "Approve / release", cancellationToken))
                            {
                                return;
                            }

                            var pending = await _approvals.GetPendingBySoNumberAsync(salesOrderId, cancellationToken);
                            if (pending is null)
                            {
                                await turnContext.SendActivityAsync(
                                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                        "VALIDATION",
                                        $"No pending release request found for sales order {salesOrderId}.")),
                                    cancellationToken);
                                return;
                            }

                            var managerSalesOrg = await _userMappingService.GetSalesOrgAsync(teamsUserId, cancellationToken);
                            var isAdmin = role == UserRole.Admin;
                            if (!isAdmin
                                && !string.IsNullOrWhiteSpace(managerSalesOrg)
                                && !string.IsNullOrWhiteSpace(pending.SalesOrg)
                                && !string.Equals(pending.SalesOrg, managerSalesOrg, StringComparison.OrdinalIgnoreCase))
                            {
                                await turnContext.SendActivityAsync(
                                    MessageFactory.Attachment(TeamsCardBuilder.BuildNotAuthorizedCard(
                                        $"Order {pending.SoNumber} belongs to sales org {pending.SalesOrg}; your scope is {managerSalesOrg}.",
                                        role.ToString(),
                                        $"Manager ({pending.SalesOrg})")),
                                    cancellationToken);
                                return;
                            }

                            // SAP: approveOrder clears delivery block; releaseOrder no longer does.
                            var updated = await _sap.ApproveOrderAsync(pending.SoNumber, linkedSapUsername, cancellationToken);
                            await _approvals.ApproveAsync(
                                pending.SoNumber,
                                linkedSapUsername,
                                managerSalesOrg,
                                isAdmin,
                                comment,
                                cancellationToken);

                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildSuccessCard(updated.SoNumber, "Approved")),
                                cancellationToken);
                        }
                        catch (UnauthorizedAccessException authEx)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildNotAuthorizedCard(
                                    authEx.Message, role.ToString(), UserRole.Manager.ToString())),
                                cancellationToken);
                        }
                        catch (InvalidOperationException invEx)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("VALIDATION", invEx.Message)),
                                cancellationToken);
                        }
                        catch (SapODataException sapEx)
                        {
                            _logger.LogError(sapEx, "SAP error approving order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("SAP_ERROR", sapEx.Message)),
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error approving order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("ACTION_FAILED", ex.Message)),
                                cancellationToken);
                        }
                        return;
                    }

                    if (string.Equals(action, "reject_approval_confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken) ? idToken.ToString() : "UNKNOWN";
                        var comment = valueObj.TryGetValue("comment", StringComparison.OrdinalIgnoreCase, out var commentToken)
                            ? commentToken.ToString()
                            : null;

                        var linkedSapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (string.IsNullOrWhiteSpace(linkedSapUsername))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "NOT_LINKED",
                                    "No SAP account is linked to your Teams identity yet.")),
                                cancellationToken);
                            return;
                        }

                        var role = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                        if (role < UserRole.Manager)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildNotAuthorizedCard(
                                    "Only Manager or Admin can reject approval requests.",
                                    role.ToString(),
                                    UserRole.Manager.ToString())),
                                cancellationToken);
                            return;
                        }

                        try
                        {
                            var managerSalesOrg = await _userMappingService.GetSalesOrgAsync(teamsUserId, cancellationToken);
                            var approval = await _approvals.RejectAsync(
                                salesOrderId,
                                linkedSapUsername,
                                managerSalesOrg,
                                isAdmin: role == UserRole.Admin,
                                comment,
                                cancellationToken);

                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildSuccessCard(approval.SoNumber, "ApprovalRejected")),
                                cancellationToken);
                        }
                        catch (UnauthorizedAccessException authEx)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildNotAuthorizedCard(
                                    authEx.Message, role.ToString(), UserRole.Manager.ToString())),
                                cancellationToken);
                        }
                        catch (InvalidOperationException invEx)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("VALIDATION", invEx.Message)),
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error rejecting approval for {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("ACTION_FAILED", ex.Message)),
                                cancellationToken);
                        }
                        return;
                    }

                    if (string.Equals(action, "reject_so_confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken) ? idToken.ToString() : "UNKNOWN";
                        var reasonCode = valueObj.TryGetValue("reasonCode", StringComparison.OrdinalIgnoreCase, out var reasonToken)
                            ? reasonToken.ToString()
                            : "OTHER";

                        var linkedSapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (string.IsNullOrWhiteSpace(linkedSapUsername))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "NOT_LINKED",
                                    "No SAP account is linked to your Teams identity yet.")),
                                cancellationToken);
                            return;
                        }

                        var sapReasonCode = SalesOrderRejectionReasons.ToSapAbgru(reasonCode);

                        try
                        {
                            if (!await EnsureLifecycleActionAllowedAsync(
                                    turnContext,
                                    salesOrderId,
                                    "Reject",
                                    cancellationToken,
                                    blockIfPendingApproval: true,
                                    blockIfNotOwner: true,
                                    currentSapUser: linkedSapUsername))
                            {
                                return;
                            }

                            var updatedOrder = await _sap.RejectOrderAsync(salesOrderId, sapReasonCode, linkedSapUsername, cancellationToken);
                            var displayedSo = string.IsNullOrWhiteSpace(updatedOrder.SoNumber)
                                              || updatedOrder.SoNumber == "UNKNOWN"
                                ? salesOrderId
                                : updatedOrder.SoNumber;
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildSuccessCard(displayedSo, "Rejected")),
                                cancellationToken);
                        }
                        catch (SapODataException sapEx)
                        {
                            _logger.LogError(sapEx, "SAP error rejecting order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("SAP_ERROR", sapEx.Message)),
                                cancellationToken: cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error rejecting order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("ACTION_FAILED", ex.Message)),
                                cancellationToken: cancellationToken);
                        }
                        return;
                    }

                    if (string.Equals(action, "cancel_so_confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken)
                            ? idToken.ToString()
                            : "UNKNOWN";
                        var reason = valueObj.TryGetValue("reason", StringComparison.OrdinalIgnoreCase, out var reasonToken)
                            ? reasonToken.ToString()?.Trim()
                            : null;

                        var linkedSapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (string.IsNullOrWhiteSpace(linkedSapUsername))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "NOT_LINKED",
                                    "No SAP account is linked to your Teams identity yet.")),
                                cancellationToken);
                            return;
                        }

                        var role = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                        try
                        {
                            if (!await EnsureLifecycleActionAllowedAsync(
                                    turnContext,
                                    salesOrderId,
                                    "Cancel",
                                    cancellationToken,
                                    blockIfPendingApproval: false,
                                    blockIfNotOwner: role < UserRole.Manager,
                                    currentSapUser: linkedSapUsername))
                            {
                                return;
                            }

                            // Manager SalesOrg scope (same as approve)
                            if (role == UserRole.Manager)
                            {
                                var order = await _sap.GetSalesOrderByIdAsync(salesOrderId, cancellationToken);
                                var managerSalesOrg = await _userMappingService.GetSalesOrgAsync(teamsUserId, cancellationToken);
                                if (order is not null
                                    && !string.IsNullOrWhiteSpace(managerSalesOrg)
                                    && !string.IsNullOrWhiteSpace(order.SalesOrg)
                                    && !string.Equals(managerSalesOrg, order.SalesOrg, StringComparison.OrdinalIgnoreCase))
                                {
                                    await turnContext.SendActivityAsync(
                                        MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                            "VALIDATION",
                                            $"Order {order.SoNumber} belongs to sales org {order.SalesOrg}; your scope is {managerSalesOrg}.")),
                                        cancellationToken);
                                    return;
                                }
                            }

                            var updatedOrder = await _sap.CancelOrderAsync(
                                salesOrderId,
                                linkedSapUsername,
                                reason,
                                cancellationToken);

                            // Clear pending release request if any
                            var pending = await _approvals.GetPendingBySoNumberAsync(updatedOrder.SoNumber, cancellationToken)
                                ?? await _approvals.GetPendingBySoNumberAsync(salesOrderId, cancellationToken);
                            if (pending is not null)
                            {
                                try
                                {
                                    var managerSalesOrg = await _userMappingService.GetSalesOrgAsync(teamsUserId, cancellationToken);
                                    await _approvals.RejectAsync(
                                        pending.SoNumber,
                                        linkedSapUsername,
                                        managerSalesOrg,
                                        isAdmin: role >= UserRole.Admin,
                                        comment: string.IsNullOrWhiteSpace(reason)
                                            ? "Order cancelled"
                                            : $"Order cancelled: {reason}",
                                        cancellationToken);
                                }
                                catch (Exception clearEx)
                                {
                                    _logger.LogWarning(
                                        clearEx,
                                        "Cancelled SO {SoNumber} but failed to clear pending approval",
                                        pending.SoNumber);
                                }
                            }

                            await _audit.LogAsync(new AuditEntry
                            {
                                TeamsUserId = teamsUserId,
                                ConversationId = conversationId,
                                Action = "CancelOrder",
                                ParametersJson = JsonConvert.SerializeObject(new
                                {
                                    order_id = updatedOrder.SoNumber,
                                    reason
                                }),
                                ResultStatus = "Success"
                            }, cancellationToken);

                            var displayedSo = string.IsNullOrWhiteSpace(updatedOrder.SoNumber)
                                              || updatedOrder.SoNumber == "UNKNOWN"
                                ? salesOrderId
                                : updatedOrder.SoNumber;
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildSuccessCard(
                                    displayedSo,
                                    "Cancelled",
                                    reason)),
                                cancellationToken);
                        }
                        catch (SapODataException sapEx)
                        {
                            _logger.LogError(sapEx, "SAP error cancelling order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("SAP_ERROR", sapEx.Message)),
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error cancelling order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("ACTION_FAILED", ex.Message)),
                                cancellationToken);
                        }

                        return;
                    }

                    if (string.Equals(action, "create_so_step1_submit", StringComparison.OrdinalIgnoreCase))
                    {
                        // ── Cascade logic: step through Org → Channel → Division ──
                        var rawSalesOrg = valueObj.TryGetValue("salesOrg", StringComparison.OrdinalIgnoreCase, out var soTok)
                            ? soTok?.ToString()?.Trim() : null;
                        var rawDistChannel = valueObj.TryGetValue("distChannel", StringComparison.OrdinalIgnoreCase, out var dcTok)
                            ? dcTok?.ToString()?.Trim() : null;
                        var rawDivision = valueObj.TryGetValue("division", StringComparison.OrdinalIgnoreCase, out var dvTok)
                            ? dvTok?.ToString()?.Trim() : null;

                        // Load the full SalesOrg list (always needed for the first dropdown).
                        IReadOnlyList<SapSalesOrg> salesOrgs;
                        try { salesOrgs = await _sap.GetSalesOrgListAsync(cancellationToken); }
                        catch { salesOrgs = Array.Empty<SapSalesOrg>(); }

                        // Step 1a: no Org selected yet — show Org dropdown only.
                        if (string.IsNullOrWhiteSpace(rawSalesOrg))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildCreateOrderStep1Card(salesOrgs)),
                                cancellationToken);
                            return;
                        }

                        var salesOrg = rawSalesOrg.ToUpperInvariant();

                        // Step 1b: Org selected, no Channel yet — show Org + Channel dropdown.
                        IReadOnlyList<SapDistChannel> distChannels;
                        try { distChannels = await _sap.GetDistChannelListAsync(salesOrg, cancellationToken); }
                        catch { distChannels = Array.Empty<SapDistChannel>(); }

                        if (string.IsNullOrWhiteSpace(rawDistChannel))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildCreateOrderStep1Card(
                                    salesOrgs, salesOrg, distChannels, null)),
                                cancellationToken);
                            return;
                        }

                        var distChannel = rawDistChannel.ToUpperInvariant();

                        // Step 1c: Org + Channel selected, no Division yet — show all three.
                        IReadOnlyList<SapDivision> divisions;
                        try { divisions = await _sap.GetDivisionListAsync(salesOrg, distChannel, cancellationToken); }
                        catch { divisions = Array.Empty<SapDivision>(); }

                        if (string.IsNullOrWhiteSpace(rawDivision))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildCreateOrderStep1Card(
                                    salesOrgs, salesOrg, distChannels, distChannel, divisions, null)),
                                cancellationToken);
                            return;
                        }

                        var division = rawDivision.ToUpperInvariant();

                        // Step 1d: all three selected — validate and advance to Step 2.
                        // Build composite keys so Step 2 can re-derive individual parts.
                        var salesAreaKey = $"{salesOrg}|{distChannel}|{division}";
                        var salesAreaLabel = $"{salesOrg} / {distChannel} / {division}";

                        IReadOnlyList<SapValidCustomer> customers;
                        IReadOnlyList<SapDocType> docTypes;
                        try
                        {
                            var custTask = _sap.GetValidCustomersAsync(salesOrg, distChannel, division, top: 500, ct: cancellationToken);
                            var doctypeTask = _sap.GetDocTypeListAsync(cancellationToken);
                            await Task.WhenAll(custTask, doctypeTask);
                            customers = custTask.Result;
                            docTypes = doctypeTask.Result;
                            if (customers.Count == 0)
                                customers = await _sap.GetValidCustomersAsync(top: 500, ct: cancellationToken);
                        }
                        catch
                        {
                            customers = Array.Empty<SapValidCustomer>();
                            docTypes = Array.Empty<SapDocType>();
                        }

                        var customerChoices = customers
                            .Select(c => new AISO.AiOrchestration.Functions.ConfirmCreateChoice(c.Label, c.Key))
                            .GroupBy(c => c.Value, StringComparer.OrdinalIgnoreCase)
                            .Select(g => g.First())
                            .ToList();

                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildCreateOrderStep2Card(
                                salesAreaLabel,
                                salesAreaKey,
                                salesOrg,
                                distChannel,
                                division,
                                customerChoices,
                                docTypes)),
                            cancellationToken);
                        return;
                    }

                    if (string.Equals(action, "create_so_step2_submit", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesAreaKey = valueObj.TryGetValue("salesArea", StringComparison.OrdinalIgnoreCase, out var areaToken)
                            ? areaToken?.ToString()?.Trim() : null;
                        var salesOrg = valueObj.TryGetValue("salesOrg", StringComparison.OrdinalIgnoreCase, out var soTok)
                            ? soTok?.ToString()?.Trim()?.ToUpperInvariant() : null;
                        var distChannel = valueObj.TryGetValue("distChannel", StringComparison.OrdinalIgnoreCase, out var dcTok)
                            ? dcTok?.ToString()?.Trim()?.ToUpperInvariant() : null;
                        var division = valueObj.TryGetValue("division", StringComparison.OrdinalIgnoreCase, out var dvTok)
                            ? dvTok?.ToString()?.Trim()?.ToUpperInvariant() : null;
                        var customerKey = valueObj.TryGetValue("customer", StringComparison.OrdinalIgnoreCase, out var custToken)
                            ? custToken?.ToString()?.Trim() : null;
                        var manualCustomerRaw = valueObj.TryGetValue("manualCustomer", StringComparison.OrdinalIgnoreCase, out var manualTok)
                            ? manualTok?.ToString()?.Trim() : null;
                        var docType = valueObj.TryGetValue("docType", StringComparison.OrdinalIgnoreCase, out var dtTok)
                            ? dtTok?.ToString()?.Trim()?.ToUpperInvariant() : null;
                        var currency = valueObj.TryGetValue("currency", StringComparison.OrdinalIgnoreCase, out var curTok)
                            ? curTok?.ToString()?.Trim()?.ToUpperInvariant() : null;
                        var purchaseOrderRef = valueObj.TryGetValue("purchaseOrderRef", StringComparison.OrdinalIgnoreCase, out var poTok)
                            ? poTok?.ToString()?.Trim() : null;
                        var requestedDeliveryDate = valueObj.TryGetValue("requestedDeliveryDate", StringComparison.OrdinalIgnoreCase, out var dateTok)
                            ? dateTok?.ToString()?.Trim() : null;
                        var shipToParty = valueObj.TryGetValue("shipToParty", StringComparison.OrdinalIgnoreCase, out var stpTok)
                            ? stpTok?.ToString()?.Trim() : null;

                        // Validate Sales Area context.
                        if (string.IsNullOrWhiteSpace(salesAreaKey)
                            || string.IsNullOrWhiteSpace(salesOrg)
                            || string.IsNullOrWhiteSpace(distChannel)
                            || string.IsNullOrWhiteSpace(division))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("VALIDATION", "Missing Sales Area context. Please go back to Step 1.")),
                                cancellationToken);
                            return;
                        }

                        // Validate DocType (required).
                        if (string.IsNullOrWhiteSpace(docType))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("VALIDATION", "Order type is required.")),
                                cancellationToken);
                            return;
                        }

                        // Validate customer.
                        string? validatedCustomerKey = null;
                        if (!string.IsNullOrWhiteSpace(customerKey)
                            && SapValidCustomer.TryParseKey(customerKey, out var custId, out _, out _, out _))
                        {
                            validatedCustomerKey = customerKey;
                        }
                        else if (!string.IsNullOrWhiteSpace(manualCustomerRaw))
                        {
                            // Manual entry: validate against SAP.
                            var manualOk = await _sap.IsCustomerValidForSalesAreaAsync(
                                manualCustomerRaw, salesOrg, distChannel, division, cancellationToken);

                            if (manualOk != true)
                            {
                                await turnContext.SendActivityAsync(
                                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                        "VALIDATION",
                                        manualOk == null
                                            ? "SAP is unavailable; cannot verify that customer."
                                            : $"Customer '{manualCustomerRaw}' is not assigned to {salesOrg} / {distChannel} / {division}.")),
                                    cancellationToken);
                                return;
                            }

                            var stripped = manualCustomerRaw.TrimStart('0');
                            validatedCustomerKey = string.IsNullOrEmpty(stripped) ? "0" : stripped;
                        }

                        if (string.IsNullOrWhiteSpace(validatedCustomerKey))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("VALIDATION", "Please select a valid Customer or type one in the manual field.")),
                                cancellationToken);
                            return;
                        }

                        // Build customer label for Step 3 display.
                        string customerLabel;
                        try
                        {
                            var custRows = await _sap.GetValidCustomersAsync(salesOrg, distChannel, division, top: 500, ct: cancellationToken);
                            var found = custRows.FirstOrDefault(c =>
                                string.Equals(c.Customer.TrimStart('0'), validatedCustomerKey.TrimStart('0'), StringComparison.OrdinalIgnoreCase)
                                || string.Equals(c.Customer, validatedCustomerKey, StringComparison.OrdinalIgnoreCase));
                            customerLabel = found?.Label ?? validatedCustomerKey.TrimStart('0');
                        }
                        catch
                        {
                            customerLabel = validatedCustomerKey.TrimStart('0');
                        }

                        // Load materials for Step 3 dropdown.
                        IReadOnlyList<SapValidMaterialSales> materials;
                        try
                        {
                            materials = await _sap.GetValidMaterialSalesAsync(salesOrg, distChannel, top: 200, ct: cancellationToken);
                            if (materials.Count == 0)
                                materials = await _sap.GetValidMaterialSalesAsync(top: 200, ct: cancellationToken);
                        }
                        catch
                        {
                            materials = Array.Empty<SapValidMaterialSales>();
                        }

                        if (materials.Count == 0)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "VALIDATION",
                                    $"No materials are available for SalesArea {salesOrg} / {distChannel} / {division}.")),
                                cancellationToken);
                            return;
                        }

                        // Build material choices: include Plant in label (Plant is part of ValidMaterialSales key).
                        IReadOnlyList<SapMaterial> materialInfos;
                        try { materialInfos = await _sap.GetMaterialsAsync(cancellationToken); } catch { materialInfos = Array.Empty<SapMaterial>(); }
                        var matDict = materialInfos.ToDictionary(m => m.Material, m => m.MaterialName, StringComparer.OrdinalIgnoreCase);

                        var materialChoices = materials
                            .Select(m =>
                            {
                                var name = !string.IsNullOrWhiteSpace(m.MaterialName)
                                    ? m.MaterialName.Trim()
                                    : (matDict.TryGetValue(m.Material, out var n) && !string.IsNullOrWhiteSpace(n)
                                        ? n.Trim()
                                        : $"Material {m.Material.TrimStart('0')}");
                                var plant = !string.IsNullOrWhiteSpace(m.Plant) ? m.Plant.Trim() : "1010";
                                var baseUnit = m.BaseUnit?.Trim().ToUpperInvariant() ?? "";
                                return new AISO.AiOrchestration.Functions.ConfirmCreateChoice(
                                    $"{m.Material.TrimStart('0')} - {name} (Plant: {plant})",
                                    $"{m.Material}|{plant}|{baseUnit}");
                            })
                            .GroupBy(c => c.Value, StringComparer.OrdinalIgnoreCase)
                            .Select(g => g.First())
                            .ToList();

                        // Persist header fields across Step 3 via card data.
                        var headerData = new Dictionary<string, object?>
                        {
                            ["salesArea"] = salesAreaKey,
                            ["salesOrg"] = salesOrg,
                            ["distChannel"] = distChannel,
                            ["division"] = division,
                            ["docType"] = docType,
                            ["currency"] = currency ?? "USD",
                            ["purchaseOrderRef"] = string.IsNullOrWhiteSpace(purchaseOrderRef) ? null : purchaseOrderRef,
                            ["requestedDeliveryDate"] = string.IsNullOrWhiteSpace(requestedDeliveryDate) ? null : requestedDeliveryDate,
                            ["shipToParty"] = string.IsNullOrWhiteSpace(shipToParty) ? null : shipToParty,
                            ["customer"] = validatedCustomerKey,
                            ["customerLabel"] = customerLabel
                        };

                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildCreateOrderStep3Card(
                                customerLabel,
                                validatedCustomerKey,
                                $"{salesOrg} / {distChannel} / {division}",
                                salesAreaKey,
                                materialChoices,
                                docType,
                                currency ?? "USD",
                                purchaseOrderRef,
                                requestedDeliveryDate,
                                shipToParty)),
                            cancellationToken);
                        return;
                    }

                    // ── Step 3: validate items → show Step 4 Review ──────────────────
                    if (string.Equals(action, "create_so_step3_submit", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesAreaKey = valueObj.TryGetValue("salesArea", StringComparison.OrdinalIgnoreCase, out var saTok)
                            ? saTok?.ToString()?.Trim() : null;
                        var salesOrg = valueObj.TryGetValue("salesOrg", StringComparison.OrdinalIgnoreCase, out var soTok)
                            ? soTok?.ToString()?.Trim()?.ToUpperInvariant() : null;
                        var distChannel = valueObj.TryGetValue("distChannel", StringComparison.OrdinalIgnoreCase, out var dcTok)
                            ? dcTok?.ToString()?.Trim()?.ToUpperInvariant() : null;
                        var division = valueObj.TryGetValue("division", StringComparison.OrdinalIgnoreCase, out var dvTok)
                            ? dvTok?.ToString()?.Trim()?.ToUpperInvariant() : null;
                        var docType = valueObj.TryGetValue("docType", StringComparison.OrdinalIgnoreCase, out var dtTok)
                            ? dtTok?.ToString()?.Trim()?.ToUpperInvariant() : null;
                        var currency = valueObj.TryGetValue("currency", StringComparison.OrdinalIgnoreCase, out var cuTok)
                            ? cuTok?.ToString()?.Trim()?.ToUpperInvariant() : "USD";
                        var purchaseOrderRef = valueObj.TryGetValue("purchaseOrderRef", StringComparison.OrdinalIgnoreCase, out var poTok)
                            ? poTok?.ToString()?.Trim() : null;
                        var requestedDeliveryDate = valueObj.TryGetValue("requestedDeliveryDate", StringComparison.OrdinalIgnoreCase, out var rdTok)
                            ? rdTok?.ToString()?.Trim() : null;
                        var shipToParty = valueObj.TryGetValue("shipToParty", StringComparison.OrdinalIgnoreCase, out var stpTok)
                            ? stpTok?.ToString()?.Trim() : null;
                        var customerId = valueObj.TryGetValue("customer", StringComparison.OrdinalIgnoreCase, out var custTok)
                            ? custTok?.ToString()?.Trim() : null;
                        var customerLabel = valueObj.TryGetValue("customerLabel", StringComparison.OrdinalIgnoreCase, out var clTok)
                            ? clTok?.ToString()?.Trim() ?? customerId ?? "" : customerId ?? "";

                        // Validate DocType.
                        if (string.IsNullOrWhiteSpace(docType))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("VALIDATION", "Order type is required.")),
                                cancellationToken);
                            return;
                        }

                        // Parse line items from unit inputs.
                        var parsedItems = new List<(string Material, decimal Qty, string Plant, string Unit)>();
                        for (var i = 1; i <= 20; i++)
                        {
                            var matKey = $"material{i}";
                            var qtyKey = $"qty{i}";
                            var unitKey = $"unit{i}";

                            var matVal = valueObj.TryGetValue(matKey, StringComparison.OrdinalIgnoreCase, out var mv) ? mv?.ToString()?.Trim() : null;
                            if (string.IsNullOrWhiteSpace(matVal))
                                continue;

                            decimal qty = 1m;
                            if (valueObj.TryGetValue(qtyKey, StringComparison.OrdinalIgnoreCase, out var qtyVal)
                                && decimal.TryParse(qtyVal?.ToString(), out var parsedQty)
                                && parsedQty > 0)
                                qty = parsedQty;
                            else
                                continue;

                            // Parse Material|Plant|Unit from dropdown value.
                            var parts = matVal.Split('|', StringSplitOptions.RemoveEmptyEntries);
                            var itemMaterial = parts.Length >= 1 ? parts[0].Trim() : matVal;
                            var itemPlant = parts.Length >= 2 ? parts[1].Trim() : "";
                            var itemUnit = valueObj.TryGetValue(unitKey, StringComparison.OrdinalIgnoreCase, out var uVal)
                                ? uVal?.ToString()?.Trim() : (parts.Length >= 3 ? parts[2].Trim() : "");

                            if (string.IsNullOrWhiteSpace(itemPlant) || string.IsNullOrWhiteSpace(itemUnit))
                            {
                                await turnContext.SendActivityAsync(
                                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                        "VALIDATION",
                                        $"Material {itemMaterial} is missing Plant or Unit. Please pick a valid material.")),
                                    cancellationToken);
                                return;
                            }

                            parsedItems.Add((itemMaterial.ToUpperInvariant(), qty, itemPlant, itemUnit.ToUpperInvariant()));
                        }

                        if (parsedItems.Count == 0)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("VALIDATION", "At least one material with qty > 0 is required.")),
                                cancellationToken);
                            return;
                        }

                        var lineItems = parsedItems
                            .Select(p => new AISO.AiOrchestration.Functions.ConfirmCreateOrderLine(
                                p.Material, p.Qty, p.Plant, p.Unit))
                            .ToList();

                        var salesAreaLabel = $"{salesOrg} / {distChannel} / {division}";
                        var customerKey = $"{customerId}|{salesOrg}|{distChannel}|{division}";

                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildCreateOrderStep4ReviewCard(
                                salesAreaLabel,
                                customerLabel,
                                shipToParty,
                                docType,
                                currency ?? "USD",
                                purchaseOrderRef,
                                requestedDeliveryDate,
                                lineItems,
                                salesAreaKey ?? "",
                                salesOrg ?? "",
                                distChannel ?? "",
                                division ?? "",
                                customerKey,
                                customerId ?? "")),
                            cancellationToken);
                        return;
                    }

                    if (string.Equals(action, "create_so_confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        var linkedSapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (string.IsNullOrWhiteSpace(linkedSapUsername))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "NOT_LINKED",
                                    "No SAP account is linked to your Teams identity yet.")),
                                cancellationToken);
                            return;
                        }

                        // ── Parse Step 4 review card data ──────────────────────────────────
                        var customerId = valueObj.TryGetValue("customer", StringComparison.OrdinalIgnoreCase, out var custToken)
                            ? custToken?.ToString()?.Trim() : null;
                        var salesOrg = valueObj.TryGetValue("salesOrg", StringComparison.OrdinalIgnoreCase, out var orgToken)
                            ? orgToken?.ToString()?.Trim()?.ToUpperInvariant() : null;
                        var distChannel = valueObj.TryGetValue("distChannel", StringComparison.OrdinalIgnoreCase, out var dcToken)
                            ? dcToken?.ToString()?.Trim()?.ToUpperInvariant() : null;
                        var division = valueObj.TryGetValue("division", StringComparison.OrdinalIgnoreCase, out var dvToken)
                            ? dvToken?.ToString()?.Trim()?.ToUpperInvariant() : null;
                        var docType = valueObj.TryGetValue("docType", StringComparison.OrdinalIgnoreCase, out var dtToken)
                            ? dtToken?.ToString()?.Trim()?.ToUpperInvariant() : null;
                        var currency = valueObj.TryGetValue("currency", StringComparison.OrdinalIgnoreCase, out var cuToken)
                            ? cuToken?.ToString()?.Trim()?.ToUpperInvariant() : "USD";
                        var purchaseOrderRef = valueObj.TryGetValue("purchaseOrderRef", StringComparison.OrdinalIgnoreCase, out var poToken)
                            ? poToken?.ToString()?.Trim() : null;
                        var requestedDeliveryDate = valueObj.TryGetValue("requestedDeliveryDate", StringComparison.OrdinalIgnoreCase, out var rdToken)
                            ? rdToken?.ToString()?.Trim() : null;
                        var shipToParty = valueObj.TryGetValue("shipToParty", StringComparison.OrdinalIgnoreCase, out var stpToken)
                            ? (stpToken?.ToString()?.Trim() is { } s && !string.Equals(s, "(default)", StringComparison.OrdinalIgnoreCase) ? s : null) : null;

                        // Fallback: try to derive from ValidCustomer key if customer is a composite key.
                        if (string.IsNullOrWhiteSpace(customerId))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("VALIDATION", "Customer is missing. Please start over.")),
                                cancellationToken);
                            return;
                        }
                        string? resolvedCustomerId = null;
                        if (SapValidCustomer.TryParseKey(customerId, out var keyCust, out _, out _, out _))
                            resolvedCustomerId = keyCust;
                        else
                            resolvedCustomerId = customerId;

                        // Fallback sales area from salesArea key if not in individual fields.
                        if (string.IsNullOrWhiteSpace(salesOrg) && valueObj.TryGetValue("salesArea", StringComparison.OrdinalIgnoreCase, out var saToken)
                            && SapSalesArea.TryParseKey(saToken?.ToString(), out var saOrg, out var saChan, out var saDiv))
                        {
                            salesOrg = saOrg;
                            distChannel = saChan;
                            division = saDiv;
                        }

                        if (string.IsNullOrWhiteSpace(salesOrg)) salesOrg = "TV01";
                        if (string.IsNullOrWhiteSpace(distChannel)) distChannel = "10";
                        if (string.IsNullOrWhiteSpace(division)) division = "00";

                        // Parse line items — prefer lineItemsJson (Step 4), fall back to individual materialN/qtyN fields.
                        var lineItemDtos = new List<CreateSalesOrderItemDto>();
                        var lineItemsJson = valueObj.TryGetValue("lineItemsJson", StringComparison.OrdinalIgnoreCase, out var ljToken)
                            ? ljToken?.ToString()?.Trim() : null;

                        if (!string.IsNullOrWhiteSpace(lineItemsJson))
                        {
                            // Step 4 confirmed — line items come as JSON.
                            try
                            {
                                var parsed = System.Text.Json.JsonSerializer.Deserialize<List<AISO.AiOrchestration.Functions.ConfirmCreateOrderLine>>(lineItemsJson);
                                if (parsed != null)
                                {
                                    foreach (var item in parsed)
                                    {
                                        var plant = !string.IsNullOrWhiteSpace(item.Plant) ? item.Plant : "1010";
                                        var unit = !string.IsNullOrWhiteSpace(item.Unit) ? item.Unit : "EA";
                                        lineItemDtos.Add(new CreateSalesOrderItemDto
                                        {
                                            Material = item.Material.ToUpperInvariant(),
                                            Plant = plant,
                                            OrderQty = item.Qty > 0 ? item.Qty : 1m,
                                            Unit = unit.ToUpperInvariant()
                                        });
                                    }
                                }
                            }
                            catch { /* fall through to field-based parsing */ }
                        }

                        if (lineItemDtos.Count == 0)
                        {
                            // Legacy fallback: parse material1..materialN / qty1..qtyN.
                            for (var i = 1; i <= AISO.AiOrchestration.Functions.CreateOrderFunction.MaxLineSlots; i++)
                            {
                                var matKey = $"material{i}";
                                var qtyKey = $"qty{i}";
                                if (i == 1 && !valueObj.ContainsKey(matKey) && valueObj.ContainsKey("material"))
                                { matKey = "material"; qtyKey = "qty"; }

                                var matVal = valueObj.TryGetValue(matKey, StringComparison.OrdinalIgnoreCase, out var mv)
                                    ? mv?.ToString()?.Trim() : null;
                                if (string.IsNullOrWhiteSpace(matVal)) continue;

                                decimal qty = 1m;
                                if (valueObj.TryGetValue(qtyKey, StringComparison.OrdinalIgnoreCase, out var qv)
                                    && decimal.TryParse(qv?.ToString(), out var parsedQty) && parsedQty > 0)
                                    qty = parsedQty;
                                else continue;

                                var parts = matVal.Split('|', StringSplitOptions.RemoveEmptyEntries);
                                var itemMat = parts.Length >= 1 ? parts[0].Trim() : matVal;
                                var itemPlant = parts.Length >= 2 ? parts[1].Trim() : "1010";
                                var itemUnit = parts.Length >= 3 ? parts[2].Trim() : "EA";

                                lineItemDtos.Add(new CreateSalesOrderItemDto
                                {
                                    Material = itemMat.ToUpperInvariant(),
                                    Plant = itemPlant,
                                    OrderQty = qty,
                                    Unit = itemUnit.ToUpperInvariant()
                                });
                            }
                        }

                        if (lineItemDtos.Count == 0)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("VALIDATION", "At least one material is required.")),
                                cancellationToken);
                            return;
                        }

                        // Validate customer.
                        var customerOk = await _sap.IsCustomerValidForSalesAreaAsync(
                            resolvedCustomerId, salesOrg, distChannel, division, cancellationToken);
                        if (customerOk == false)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "VALIDATION",
                                    $"Customer {resolvedCustomerId} is not valid for sales area {salesOrg}/{distChannel}/{division}.")),
                                cancellationToken);
                            return;
                        }

                        // Validate Plant/Unit per line.
                        foreach (var item in lineItemDtos)
                        {
                            if (string.IsNullOrWhiteSpace(item.Plant) || string.IsNullOrWhiteSpace(item.Unit))
                            {
                                await turnContext.SendActivityAsync(
                                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                        "VALIDATION",
                                        $"Material {item.Material} is missing Plant or Unit.")),
                                    cancellationToken);
                                return;
                            }
                        }

                        try
                        {
                            var created = await _sap.CreateSalesOrderAsync(
                                new CreateSalesOrderDto
                                {
                                    DocType = string.IsNullOrWhiteSpace(docType) ? "TA" : docType,
                                    SalesOrg = salesOrg,
                                    DistChannel = distChannel,
                                    Division = division,
                                    Customer = resolvedCustomerId,
                                    Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency,
                                    ShipToParty = shipToParty,
                                    RequestingSapUser = linkedSapUsername,
                                    PurchaseOrderRef = string.IsNullOrWhiteSpace(purchaseOrderRef) ? null : purchaseOrderRef,
                                    RequestedDeliveryDate = string.IsNullOrWhiteSpace(requestedDeliveryDate) ? null : requestedDeliveryDate,
                                    Items = lineItemDtos
                                },
                                cancellationToken);

                            var linesSummary = string.Join(", ", lineItemDtos.Select(i => $"{i.Material} x {i.OrderQty:0}"));

                            await _audit.LogAsync(new AuditEntry
                            {
                                TeamsUserId = teamsUserId,
                                ConversationId = conversationId,
                                Action = "CreateOrder",
                                ParametersJson = JsonConvert.SerializeObject(new
                                {
                                    order_id = created.SoNumber,
                                    customer = resolvedCustomerId,
                                    sales_org = salesOrg,
                                    docType,
                                    currency,
                                    shipToParty,
                                    items = lineItemDtos.Select(i => new { i.Material, qty = i.OrderQty })
                                }),
                                ResultStatus = "Success"
                            }, cancellationToken);

                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildSuccessCard(
                                    created.SoNumber,
                                    "Created",
                                    $"{resolvedCustomerId} · {linesSummary}")),
                                cancellationToken);

                            var roleForDetail = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(await BuildSalesOrderDetailAttachmentAsync(
                                    created,
                                    roleForDetail,
                                    linkedSapUsername,
                                    cancellationToken)),
                                cancellationToken);
                        }
                        catch (SapODataException sapEx)
                        {
                            var errorCode = sapEx.IsValidationError ? "VALIDATION" : "SAP_ERROR";
                            _logger.LogError(sapEx, "SAP error creating sales order (classified={ErrorCode})", errorCode);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(errorCode, sapEx.Message)),
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error creating sales order");
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("ACTION_FAILED", ex.Message)),
                                cancellationToken);
                        }

                        return;
                    }

                    if (string.Equals(action, "update_ref_confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken)
                            ? idToken.ToString()
                            : "UNKNOWN";
                        var newReference = valueObj.TryGetValue("newReference", StringComparison.OrdinalIgnoreCase, out var refToken)
                            ? refToken.ToString()?.Trim()
                            : null;

                        var linkedSapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (string.IsNullOrWhiteSpace(linkedSapUsername))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "NOT_LINKED",
                                    "No SAP account is linked to your Teams identity yet.")),
                                cancellationToken);
                            return;
                        }

                        if (string.IsNullOrWhiteSpace(newReference))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "VALIDATION",
                                    "New reference is required.")),
                                cancellationToken);
                            return;
                        }

                        try
                        {
                            if (!await EnsureLifecycleActionAllowedAsync(
                                    turnContext,
                                    salesOrderId,
                                    "Update reference",
                                    cancellationToken,
                                    blockIfPendingApproval: true,
                                    blockIfNotOwner: true,
                                    currentSapUser: linkedSapUsername))
                            {
                                return;
                            }

                            var updated = await _sap.UpdateReferenceAsync(
                                salesOrderId,
                                newReference,
                                linkedSapUsername,
                                cancellationToken);

                            await _audit.LogAsync(new AuditEntry
                            {
                                TeamsUserId = teamsUserId,
                                ConversationId = conversationId,
                                Action = "UpdateOrderReference",
                                ParametersJson = JsonConvert.SerializeObject(new
                                {
                                    order_id = updated.SoNumber,
                                    new_reference = newReference
                                }),
                                ResultStatus = "Success"
                            }, cancellationToken);

                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildSuccessCard(
                                    updated.SoNumber,
                                    "ReferenceUpdated",
                                    newReference)),
                                cancellationToken);

                            var roleForDetail = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(await BuildSalesOrderDetailAttachmentAsync(
                                    updated,
                                    roleForDetail,
                                    linkedSapUsername,
                                    cancellationToken)),
                                cancellationToken);
                        }
                        catch (SapODataException sapEx)
                        {
                            _logger.LogError(sapEx, "SAP error updating reference for {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("SAP_ERROR", sapEx.Message)),
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error updating reference for {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("ACTION_FAILED", ex.Message)),
                                cancellationToken);
                        }

                        return;
                    }

                    if (string.Equals(action, "edit_so_confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken)
                            ? idToken.ToString()
                            : "UNKNOWN";
                        var newReference = valueObj.TryGetValue("newReference", StringComparison.OrdinalIgnoreCase, out var refToken)
                            ? refToken.ToString()?.Trim()
                            : null;
                        var reqDeliveryDate = valueObj.TryGetValue("reqDeliveryDate", StringComparison.OrdinalIgnoreCase, out var dateToken)
                            ? dateToken.ToString()?.Trim()
                            : null;
                        var lineOp = valueObj.TryGetValue("lineOp", StringComparison.OrdinalIgnoreCase, out var opToken)
                            ? opToken.ToString()?.Trim().ToUpperInvariant() ?? "NONE"
                            : "NONE";
                        var itemNumber = valueObj.TryGetValue("itemNumber", StringComparison.OrdinalIgnoreCase, out var itemToken)
                            ? itemToken.ToString()?.Trim()
                            : null;
                        var material = valueObj.TryGetValue("material", StringComparison.OrdinalIgnoreCase, out var matToken)
                            ? matToken.ToString()?.Trim()
                            : null;
                        var plant = valueObj.TryGetValue("plant", StringComparison.OrdinalIgnoreCase, out var plantToken)
                            ? plantToken.ToString()?.Trim()
                            : null;
                        var unit = valueObj.TryGetValue("unit", StringComparison.OrdinalIgnoreCase, out var unitToken)
                            ? unitToken.ToString()?.Trim()
                            : null;

                        decimal? qty = null;
                        if (valueObj.TryGetValue("qty", StringComparison.OrdinalIgnoreCase, out var qtyToken)
                            && decimal.TryParse(qtyToken.ToString(), out var parsedQty))
                        {
                            qty = parsedQty;
                        }

                        var linkedSapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (string.IsNullOrWhiteSpace(linkedSapUsername))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "NOT_LINKED",
                                    "No SAP account is linked to your Teams identity yet.")),
                                cancellationToken);
                            return;
                        }

                        var role = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                        try
                        {
                            if (!await EnsureLifecycleActionAllowedAsync(
                                    turnContext,
                                    salesOrderId,
                                    "Edit",
                                    cancellationToken,
                                    blockIfPendingApproval: role < UserRole.Manager,
                                    blockIfNotOwner: role < UserRole.Manager,
                                    currentSapUser: linkedSapUsername))
                            {
                                return;
                            }

                            if (role == UserRole.Manager)
                            {
                                var orderScope = await _sap.GetSalesOrderByIdAsync(salesOrderId, cancellationToken);
                                var managerSalesOrg = await _userMappingService.GetSalesOrgAsync(teamsUserId, cancellationToken);
                                if (orderScope is not null
                                    && !string.IsNullOrWhiteSpace(managerSalesOrg)
                                    && !string.IsNullOrWhiteSpace(orderScope.SalesOrg)
                                    && !string.Equals(managerSalesOrg, orderScope.SalesOrg, StringComparison.OrdinalIgnoreCase))
                                {
                                    await turnContext.SendActivityAsync(
                                        MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                            "VALIDATION",
                                            $"Order {orderScope.SoNumber} belongs to sales org {orderScope.SalesOrg}; your scope is {managerSalesOrg}.")),
                                        cancellationToken);
                                    return;
                                }
                            }

                            var existing = await _sap.GetSalesOrderByIdAsync(salesOrderId, cancellationToken);
                            var changeRef = !string.IsNullOrWhiteSpace(newReference)
                                && !string.Equals(
                                    newReference,
                                    existing?.CustomerReference?.Trim() ?? string.Empty,
                                    StringComparison.Ordinal);
                            var currentDate = existing?.RequestedDeliveryDate?.ToString("yyyy-MM-dd") ?? string.Empty;
                            var changeDate = !string.IsNullOrWhiteSpace(reqDeliveryDate)
                                && !string.Equals(reqDeliveryDate, currentDate, StringComparison.Ordinal);

                            var items = new List<UpdateSalesOrderItemDto>();

                            // Add line items if operation is specified (U/I/D)
                            if (!string.IsNullOrWhiteSpace(lineOp) && lineOp is not ("NONE" or "N"))
                            {
                                if (lineOp is "U" or "I")
                                {
                                    if (lineOp == "I" && string.IsNullOrWhiteSpace(material))
                                    {
                                        await turnContext.SendActivityAsync(
                                            MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                                "VALIDATION",
                                                "Material is required when adding a line.")),
                                            cancellationToken);
                                        return;
                                    }

                                    if (qty is null or <= 0)
                                    {
                                        await turnContext.SendActivityAsync(
                                            MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                                "VALIDATION",
                                                "Quantity must be greater than 0 for line update/add.")),
                                            cancellationToken);
                                        return;
                                    }
                                }

                                if ((lineOp is "U" or "D") && string.IsNullOrWhiteSpace(itemNumber))
                                {
                                    await turnContext.SendActivityAsync(
                                        MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                            "VALIDATION",
                                            "Item number is required for update/delete.")),
                                        cancellationToken);
                                    return;
                                }

                                items.Add(new UpdateSalesOrderItemDto
                                {
                                    Operation = lineOp,
                                    ItemNumber = itemNumber,
                                    Material = material,
                                    Plant = string.IsNullOrWhiteSpace(plant) ? "1010" : plant,
                                    OrderQty = qty,
                                    Unit = string.IsNullOrWhiteSpace(unit) ? "PC" : unit
                                });
                            }

                            // Pre-check: skip SAP call if nothing actually changed
                            var hasItemsToChange = items.Count > 0 && items.Any(i =>
                                !string.IsNullOrWhiteSpace(i.Operation) &&
                                i.Operation is not ("NONE" or "N"));

                            if (!changeRef && !changeDate && !hasItemsToChange)
                            {
                                await turnContext.SendActivityAsync(
                                    MessageFactory.Text("Nothing to update — no changes were made to the sales order."),
                                    cancellationToken);
                                return;
                            }

                            // Allow submit even if no changes — SAP will handle gracefully
                            // (null fields = no change). Show info only when truly nothing changed.

                            var updated = await _sap.UpdateSalesOrderAsync(
                                new UpdateSalesOrderDto
                                {
                                    SoNumber = salesOrderId,
                                    RequestingSapUser = linkedSapUsername,
                                    PurchaseOrderRef = changeRef ? newReference : null,
                                    ReqDeliveryDate = changeDate ? reqDeliveryDate : null,
                                    Items = items
                                },
                                cancellationToken);

                            await _audit.LogAsync(new AuditEntry
                            {
                                TeamsUserId = teamsUserId,
                                ConversationId = conversationId,
                                Action = "EditOrder",
                                ParametersJson = JsonConvert.SerializeObject(new
                                {
                                    order_id = updated.SoNumber,
                                    new_reference = changeRef ? newReference : null,
                                    req_delivery_date = changeDate ? reqDeliveryDate : null,
                                    line_op = lineOp,
                                    item_no = itemNumber
                                }),
                                ResultStatus = "Success"
                            }, cancellationToken);

                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildSuccessCard(
                                    updated.SoNumber,
                                    "Updated",
                                    "Sales order changes saved")),
                                cancellationToken);

                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(await BuildSalesOrderDetailAttachmentAsync(
                                    updated,
                                    role,
                                    linkedSapUsername,
                                    cancellationToken)),
                                cancellationToken);
                        }
                        catch (SapODataException sapEx)
                        {
                            var errorCode = sapEx.IsValidationError ? "VALIDATION" : "SAP_ERROR";
                            _logger.LogError(sapEx, "SAP error editing order {OrderId} (classified={ErrorCode})", salesOrderId, errorCode);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(errorCode, sapEx.Message)),
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error editing order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("ACTION_FAILED", ex.Message)),
                                cancellationToken);
                        }

                        return;
                    }

                    if (string.Equals(action, "force_cancel_confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken)
                            ? idToken.ToString()
                            : "UNKNOWN";
                        var reason = valueObj.TryGetValue("reason", StringComparison.OrdinalIgnoreCase, out var reasonToken)
                            ? reasonToken.ToString()?.Trim()
                            : null;

                        var role = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                        if (role != UserRole.Admin)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildNotAuthorizedCard(
                                    "Only administrators can force cancel sales orders.",
                                    role.ToString(),
                                    "Admin")),
                                cancellationToken);
                            return;
                        }

                        var linkedSapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (string.IsNullOrWhiteSpace(linkedSapUsername))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "NOT_LINKED",
                                    "No SAP account is linked to your Teams identity yet.")),
                                cancellationToken);
                            return;
                        }

                        if (string.IsNullOrWhiteSpace(reason))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "VALIDATION",
                                    "Override reason is required for force cancel.")),
                                cancellationToken);
                            return;
                        }

                        try
                        {
                            if (!await EnsureLifecycleActionAllowedAsync(
                                    turnContext,
                                    salesOrderId,
                                    "Force cancel",
                                    cancellationToken))
                            {
                                return;
                            }

                            var updated = await _sap.ForceCancelAsync(
                                salesOrderId,
                                linkedSapUsername,
                                reason,
                                cancellationToken);

                            await _audit.LogAsync(new AuditEntry
                            {
                                TeamsUserId = teamsUserId,
                                ConversationId = conversationId,
                                Action = "ForceCancel",
                                ParametersJson = JsonConvert.SerializeObject(new
                                {
                                    order_id = updated.SoNumber,
                                    reason
                                }),
                                ResultStatus = "Success"
                            }, cancellationToken);

                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildSuccessCard(
                                    updated.SoNumber,
                                    "ForceCancelled",
                                    reason)),
                                cancellationToken);
                        }
                        catch (SapODataException sapEx)
                        {
                            _logger.LogError(sapEx, "SAP error force-cancelling order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("SAP_ERROR", sapEx.Message)),
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error force-cancelling order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("ACTION_FAILED", ex.Message)),
                                cancellationToken);
                        }

                        return;
                    }

                    if (string.Equals(action, "force_release_confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken)
                            ? idToken.ToString()
                            : "UNKNOWN";
                        var reason = valueObj.TryGetValue("reason", StringComparison.OrdinalIgnoreCase, out var reasonToken)
                            ? reasonToken.ToString()?.Trim()
                            : null;

                        var role = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                        if (role != UserRole.Admin)
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildNotAuthorizedCard(
                                    "Only administrators can force release sales orders.",
                                    role.ToString(),
                                    "Admin")),
                                cancellationToken);
                            return;
                        }

                        var linkedSapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (string.IsNullOrWhiteSpace(linkedSapUsername))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "NOT_LINKED",
                                    "No SAP account is linked to your Teams identity yet.")),
                                cancellationToken);
                            return;
                        }

                        if (string.IsNullOrWhiteSpace(reason))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "VALIDATION",
                                    "Override reason is required for force release.")),
                                cancellationToken);
                            return;
                        }

                        try
                        {
                            if (!await EnsureLifecycleActionAllowedAsync(
                                    turnContext,
                                    salesOrderId,
                                    "Force release",
                                    cancellationToken))
                            {
                                return;
                            }

                            var updated = await _sap.ForceReleaseAsync(
                                salesOrderId,
                                linkedSapUsername,
                                reason,
                                cancellationToken);

                            await _audit.LogAsync(new AuditEntry
                            {
                                TeamsUserId = teamsUserId,
                                ConversationId = conversationId,
                                Action = "ForceRelease",
                                ParametersJson = JsonConvert.SerializeObject(new
                                {
                                    order_id = updated.SoNumber,
                                    reason
                                }),
                                ResultStatus = "Success"
                            }, cancellationToken);

                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildSuccessCard(
                                    updated.SoNumber,
                                    "ForceReleased",
                                    reason)),
                                cancellationToken);
                        }
                        catch (SapODataException sapEx)
                        {
                            _logger.LogError(sapEx, "SAP error force-releasing order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("SAP_ERROR", sapEx.Message)),
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error force-releasing order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("ACTION_FAILED", ex.Message)),
                                cancellationToken);
                        }

                        return;
                    }

                    if (string.Equals(action, "forward_so_confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        var salesOrderId = valueObj.TryGetValue("salesOrderId", StringComparison.OrdinalIgnoreCase, out var idToken) ? idToken.ToString() : "UNKNOWN";
                        var forwardToUser = valueObj.TryGetValue("forwardToUser", StringComparison.OrdinalIgnoreCase, out var forwardToken)
                            ? forwardToken.ToString()
                            : null;
                        var remarks = valueObj.TryGetValue("comment", StringComparison.OrdinalIgnoreCase, out var commentToken)
                            ? commentToken.ToString()
                            : null;

                        var linkedSapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
                        if (string.IsNullOrWhiteSpace(linkedSapUsername))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "NOT_LINKED",
                                    "No SAP account is linked to your Teams identity yet.")),
                                cancellationToken);
                            return;
                        }

                        if (string.IsNullOrWhiteSpace(forwardToUser))
                        {
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                                    "VALIDATION",
                                    "Please select a recipient before forwarding the order.")),
                                cancellationToken);
                            return;
                        }

                        try
                        {
                            if (!await EnsureLifecycleActionAllowedAsync(
                                    turnContext,
                                    salesOrderId,
                                    "Forward",
                                    cancellationToken,
                                    blockIfPendingApproval: true,
                                    blockIfNotOwner: true,
                                    currentSapUser: linkedSapUsername))
                            {
                                return;
                            }

                            var updatedOrder = await _sap.ForwardOrderAsync(
                                salesOrderId,
                                forwardToUser,
                                linkedSapUsername,
                                cancellationToken,
                                remarks);
                            var displayedSo = updatedOrder.SoNumber == "UNKNOWN"
                                ? salesOrderId
                                : updatedOrder.SoNumber;
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(
                                    TeamsCardBuilder.BuildSuccessCard(displayedSo, "Forwarded", forwardToUser)),
                                cancellationToken: cancellationToken);
                        }
                        catch (SapODataException sapEx)
                        {
                            _logger.LogError(sapEx, "SAP error forwarding order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("SAP_ERROR", sapEx.Message)),
                                cancellationToken: cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error forwarding order {OrderId}", salesOrderId);
                            await turnContext.SendActivityAsync(
                                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("ACTION_FAILED", ex.Message)),
                                cancellationToken: cancellationToken);
                        }
                        return;
                    }

                    if (string.Equals(action, "view_revenue_kpi", StringComparison.OrdinalIgnoreCase))
                    {
                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildKpiRevenueCard(new
                            {
                                period = "This month",
                                totalRevenue = "$245K",
                                growthRate = "+12%",
                                targetRevenue = "$220K",
                                chartUrl = "https://quickchart.io/chart?c=eyJ0eXBlIjoiZG91Z2hudXQiLCJkYXRhIjp7ImxhYmVscyI6WyJNYXJjaCJdLCJkYXRhc2V0cyI6W3siZGF0YSI6WzI0NSJdfV19"
                            })),
                            cancellationToken);
                        return;
                    }

                    if (string.Equals(action, "view_delivery_kpi", StringComparison.OrdinalIgnoreCase))
                    {
                        await turnContext.SendActivityAsync(
                            MessageFactory.Attachment(TeamsCardBuilder.BuildKpiDeliveryCard(new
                            {
                                onTimeRate = "94%",
                                delayedCount = "12",
                                completedToday = "24",
                                deliveryProgress = 94,
                                chartUrl = "https://quickchart.io/chart?c=eyJ0eXBlIjoicG9sYXIiLCJkYXRhIjp7ImxhYmVscyI6WyJQcm9ncmVzcyJdLCJkYXRhc2V0cyI6W3siZGF0YSI6Wzk0XX1dfQ=="
                            })),
                            cancellationToken);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in Activity.Value parsing or action execution.");
                await turnContext.SendActivityAsync($"DEV ERROR: {ex.Message} \n {ex.StackTrace}", cancellationToken: cancellationToken);
            }
        }

        var normalizedMessage = userMessage.Trim();
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            var debugVal = turnContext.Activity.Value != null ? Newtonsoft.Json.JsonConvert.SerializeObject(turnContext.Activity.Value) : "NULL";
            await turnContext.SendActivityAsync(
                $"DEBUG - Activity.Value was: {debugVal}",
                cancellationToken: cancellationToken);
            return;
        }

        // Push activity-scoped properties into Serilog LogContext so every
        // log emitted inside this turn is tagged for end-to-end traceability.
        using (LogContext.PushProperty("ActivityId", activityId))
        using (LogContext.PushProperty("ConversationId", conversationId))
        using (LogContext.PushProperty("UserId", teamsUserId))
        {
            _logger.LogInformation(
                "Bot received message: {UserMessage}", userMessage);

            if (IsHelpIntent(normalizedMessage))
            {
                var currentRole = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
                var roleName = currentRole switch
                {
                    UserRole.Admin => "Admin",
                    UserRole.Manager => "Manager",
                    _ => "Employee"
                };

                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(TeamsCardBuilder.BuildHelpCard(roleName)),
                    cancellationToken);
                return;
            }
            if (string.Equals(normalizedMessage, "delegate approval", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedMessage, "ủy quyền", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedMessage, "uy quyen", StringComparison.OrdinalIgnoreCase))
            {
                var todayStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var tomorrowStr = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");

                var rawChoices = await _userMappingService.GetForwardRecipientChoicesAsync(cancellationToken);

                var managerChoices = rawChoices.Select(c => new { title = c.Title, value = c.Value });

                var delegateFormCard = TeamsCardBuilder.BuildConfirmDelegateApprovalCard(
                    delegateUser: "",
                    validFromRaw: todayStr,
                    validToRaw: tomorrowStr,
                    validFrom: todayStr,
                    validTo: tomorrowStr,
                    reason: "",
                    maxAmountRaw: "10000000",
                    maxAmount: "10,000,000",
                    currency: "VND",
                    managerChoices: managerChoices);

                await turnContext.SendActivityAsync(MessageFactory.Attachment(delegateFormCard), cancellationToken);
                return;
            }
            if (string.Equals(normalizedMessage, "cancel", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedMessage, "thoát", StringComparison.OrdinalIgnoreCase))
            {
                await _conversationState.ClearStateAsync(turnContext, cancellationToken);
                await turnContext.SendActivityAsync(
                    "Cancelled the current flow. You can start again.",
                    cancellationToken: cancellationToken);
                return;
            }

            if (string.Equals(normalizedMessage, "logout", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedMessage, "đăng xuất", StringComparison.OrdinalIgnoreCase))
            {
                await _userMappingService.RemoveMappingAsync(teamsUserId, cancellationToken);
                await turnContext.SendActivityAsync(
                    "Signed out of your SAP account. Type hi to sign in again.",
                    cancellationToken: cancellationToken);
                return;
            }

            if (await TryHandleOrderDetailRequest(normalizedMessage, turnContext, cancellationToken))
            {
                return;
            }

            var sapUsername = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);

            var dialogSet = new DialogSet(_conversationState.CreateProperty<DialogState>("DialogState"));
            dialogSet.Add(_dialog);
            var dialogContext = await dialogSet.CreateContextAsync(turnContext, cancellationToken);

            if (dialogContext.ActiveDialog != null || string.IsNullOrEmpty(sapUsername))
            {
                // We are either in the middle of login/mapping OR we need to start it
                await _dialog.RunAsync(turnContext, _conversationState.CreateProperty<DialogState>("DialogState"), cancellationToken);
                return;
            }

            var loadingActivity = await turnContext.SendActivityAsync(
                MessageFactory.Attachment(TeamsCardBuilder.BuildLoadingCard()),
                cancellationToken);
            var loadingActivityId = loadingActivity?.Id;

            var role = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);

            var stopwatch = Stopwatch.StartNew();
            var dispatch = await _dispatcher.DispatchAsync(userMessage, sapUsername, role, cancellationToken);
            stopwatch.Stop();

            // Audit — best-effort: a write failure must not break the bot.
            try
            {
                await _audit.LogAsync(new AuditEntry
                {
                    TeamsUserId = teamsUserId,
                    ConversationId = conversationId,
                    Action = dispatch.FunctionName ?? "unrecognized",
                    ParametersJson = dispatch.ParametersJson,
                    ResultStatus = DeriveStatus(dispatch),
                    DurationMs = (int)stopwatch.ElapsedMilliseconds,
                    ErrorMessage = dispatch.Result?.ErrorMessage ?? dispatch.Reason
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write audit log entry");
            }

            if (!dispatch.Handled)
            {
                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildErrorCard("UNHANDLED", dispatch.Reason ?? "Unknown request"),
                    cancellationToken);
                return;
            }

            if (dispatch.Denied)
            {
                var required = dispatch.FunctionName is not null
                    ? RolePolicy.RequiredRole(dispatch.FunctionName).ToString()
                    : UserRole.Manager.ToString();

                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildNotAuthorizedCard(
                        dispatch.Result?.ErrorMessage ?? "You are not authorized to perform this action.",
                        role.ToString(),
                        required),
                    cancellationToken);

                _logger.LogWarning(
                    "Blocked {Function} for user {TeamsUserId} (role {Role})",
                    dispatch.FunctionName, teamsUserId, role);
                return;
            }

            if (dispatch.Result is not { Success: true } result)
            {
                _logger.LogWarning(
                    "Function {Function} returned failure: {Error}",
                    dispatch.FunctionName, dispatch.Result?.ErrorMessage);

                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildErrorCard(
                        dispatch.Result?.ErrorCode ?? "FUNCTION_FAILED",
                        dispatch.Result?.ErrorMessage ?? "Unknown error"),
                    cancellationToken);
                return;
            }

            if (result.Payload is string textReply)
            {
                await ReplaceLoadingActivityAsync(turnContext, loadingActivityId, textReply, cancellationToken);

                _logger.LogInformation("Bot replied with AI text response");
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.MyProfileResponse myProfileResponse)
            {
                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildMyProfileCard(myProfileResponse),
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with My profile card for {SapUser} (total={Total})",
                    myProfileResponse.SapUser,
                    myProfileResponse.Counts.Total);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.GetSalesOrdersResponse getOrdersResponse)
            {
                var orders = getOrdersResponse.Orders;
                if (orders.Count == 0)
                {
                    await ReplaceLoadingActivityAsync(
                        turnContext,
                        loadingActivityId,
                        TeamsCardBuilder.BuildEmptyCard(),
                        cancellationToken);
                    return;
                }

                var kpiCard = TeamsCardBuilder.BuildKpiCardForRequest(normalizedMessage, orders, getOrdersResponse.ChartUrl);
                if (kpiCard is not null)
                {
                    await ReplaceLoadingActivityAsync(
                        turnContext,
                        loadingActivityId,
                        kpiCard,
                        cancellationToken);

                    _logger.LogInformation(
                        "Bot replied with KPI card for request '{Request}' using data from GetSalesOrdersFunction",
                        normalizedMessage);
                    return;
                }

                var latestBySo = await GetLatestApprovalsBySoAsync(orders, cancellationToken);
                var card = TeamsCardBuilder.BuildSoSummaryCard(orders, getOrdersResponse.Title, latestBySo);
                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    card,
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with Adaptive Card listing {Count} orders", orders.Count);
                return;
            }

            if (result.Payload is IReadOnlyList<Domain.SalesOrders.SalesOrder> ordersList)
            {
                if (ordersList.Count == 0)
                {
                    await ReplaceLoadingActivityAsync(
                        turnContext,
                        loadingActivityId,
                        TeamsCardBuilder.BuildEmptyCard(),
                        cancellationToken);
                    return;
                }

                // Single-order lookups (CheckOrderStatus / GetOrderDetail) → detail card + pending banner.
                if (ordersList.Count == 1
                    && IsOrderDetailFunction(dispatch.FunctionName))
                {
                    var order = ordersList[0];
                    var detailCard = await BuildSalesOrderDetailAttachmentAsync(
                        order,
                        role,
                        sapUsername,
                        cancellationToken);

                    await ReplaceLoadingActivityAsync(
                        turnContext,
                        loadingActivityId,
                        detailCard,
                        cancellationToken);

                    _logger.LogInformation(
                        "Bot replied with sales order detail card for {Function} SO {SoNumber}",
                        dispatch.FunctionName,
                        order.SoNumber);
                    return;
                }

                var latestBySo = await GetLatestApprovalsBySoAsync(ordersList, cancellationToken);
                var card = TeamsCardBuilder.BuildSoSummaryCard(ordersList, latestApprovalsBySo: latestBySo);
                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    card,
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with Adaptive Card listing {Count} orders from {Function}",
                    ordersList.Count,
                    dispatch.FunctionName);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.GetKpiSummaryResponse kpiSummaryResponse)
            {
                var summary = kpiSummaryResponse.Summary;
                var kpiSummaryCard = TeamsCardBuilder.BuildKpiSummaryCard(new
                {
                    period = string.IsNullOrWhiteSpace(summary.Period) ? "All time" : summary.Period,
                    revenueValue = $"{summary.TotalRevenue:N0} {summary.Currency}",
                    orderCount = summary.TotalOrders,
                    openOrders = summary.OpenOrders,
                    deliveredOrders = summary.DeliveredOrders,
                    overdueOrders = summary.OverdueOrders,
                    fulfillmentRate = $"{summary.FulfillmentRate:0.#}%",
                    chartUrl = kpiSummaryResponse.ChartUrl ?? string.Empty
                });

                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    kpiSummaryCard,
                    cancellationToken);

                _logger.LogInformation("Bot replied with KPI summary card");
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.GetKpiByCustomerResponse kpiByCustomerResponse)
            {
                if (kpiByCustomerResponse.Customers.Count == 0)
                {
                    await ReplaceLoadingActivityAsync(
                        turnContext,
                        loadingActivityId,
                        TeamsCardBuilder.BuildEmptyCard(),
                        cancellationToken);
                    return;
                }

                var kpiByCustomerCard = TeamsCardBuilder.BuildKpiByCustomerCard(new
                {
                    count = kpiByCustomerResponse.Customers.Count,
                    chartUrl = kpiByCustomerResponse.ChartUrl ?? string.Empty,
                    customers = kpiByCustomerResponse.Customers.Select(c => new
                    {
                        customerId = string.IsNullOrWhiteSpace(c.CustomerId) ? "-" : c.CustomerId,
                        customerName = string.IsNullOrWhiteSpace(c.CustomerName) ? c.CustomerId : c.CustomerName,
                        orderCount = c.OrderCount,
                        fulfillmentRate = $"{c.FulfillmentRate:0.#}%",
                        formattedRevenue = $"{c.Revenue:N0} {c.Currency}"
                    }).ToList()
                });

                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    kpiByCustomerCard,
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with KPI by customer card ({Count})",
                    kpiByCustomerResponse.Customers.Count);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.GetKpiByProductResponse kpiByProductResponse)
            {
                if (kpiByProductResponse.Products.Count == 0)
                {
                    await ReplaceLoadingActivityAsync(
                        turnContext,
                        loadingActivityId,
                        TeamsCardBuilder.BuildEmptyCard(),
                        cancellationToken);
                    return;
                }

                var kpiByProductCard = TeamsCardBuilder.BuildKpiByProductCard(new
                {
                    count = kpiByProductResponse.Products.Count,
                    chartUrl = kpiByProductResponse.ChartUrl ?? string.Empty,
                    products = kpiByProductResponse.Products.Select(p => new
                    {
                        materialId = string.IsNullOrWhiteSpace(p.MaterialId) ? "-" : p.MaterialId,
                        materialName = string.IsNullOrWhiteSpace(p.MaterialName) ? p.MaterialId : p.MaterialName,
                        orderCount = p.OrderCount,
                        formattedQty = $"{p.TotalQty:N0} {p.Unit}",
                        formattedRevenue = $"{p.Revenue:N0} {p.Currency}"
                    }).ToList()
                });

                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    kpiByProductCard,
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with KPI by product card ({Count})",
                    kpiByProductResponse.Products.Count);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.GetPendingApprovalsResponse pendingResponse)
            {
                if (pendingResponse.Count == 0)
                {
                    await ReplaceLoadingActivityAsync(
                        turnContext,
                        loadingActivityId,
                        TeamsCardBuilder.BuildEmptyCard(),
                        cancellationToken);
                    return;
                }

                var pendingCard = TeamsCardBuilder.BuildPendingApprovalsCard(new
                {
                    count = pendingResponse.Count,
                    search = string.Empty,
                    selectedRequester = string.Empty,
                    requesterChoices = pendingResponse.Items
                        .Select(i => i.RequestedBy)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value)
                        .Select(value => new { title = value, value })
                        .ToList(),
                    items = pendingResponse.Items.Select(i => new
                    {
                        orderId = i.OrderId,
                        requestedBy = i.RequestedBy,
                        comment = i.Comment ?? string.Empty,
                        requestedAt = i.RequestedAt
                    }).ToList()
                });

                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    pendingCard,
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with pending approvals card ({Count})", pendingResponse.Count);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.ViewAuditLogResponse auditResponse)
            {
                if (auditResponse.Count == 0)
                {
                    await ReplaceLoadingActivityAsync(
                        turnContext,
                        loadingActivityId,
                        TeamsCardBuilder.BuildEmptyCard(),
                        cancellationToken);
                    return;
                }

                var userLabels = await _userMappingService.GetAuditUserLabelsAsync(
                    auditResponse.Items.Select(i => i.TeamsUserId),
                    cancellationToken);

                var auditCard = TeamsCardBuilder.BuildAuditLogCard(new
                {
                    count = auditResponse.Count,
                    items = auditResponse.Items.Select(i =>
                    {
                        userLabels.TryGetValue(i.TeamsUserId, out var label);
                        return new
                        {
                            timestamp = i.Timestamp,
                            action = Domain.Auditing.AuditLogDisplay.FriendlyAction(i.Action),
                            status = i.Status,
                            user = label
                                ?? Domain.Auditing.AuditLogDisplay.FormatUserLabel(null, null, i.TeamsUserId),
                            duration = Domain.Auditing.AuditLogDisplay.FormatDuration(i.DurationMs),
                            error = i.Error ?? string.Empty
                        };
                    }).ToList()
                });

                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    auditCard,
                    cancellationToken);

                _logger.LogInformation("Bot replied with audit log card ({Count})", auditResponse.Count);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.ListDelegationsResponse listDelegationsResponse)
            {
                if (listDelegationsResponse.Delegations.Count == 0)
                {
                    await ReplaceLoadingActivityAsync(
                        turnContext,
                        loadingActivityId,
                        "There are currently no active delegations.",
                        cancellationToken);
                    return;
                }

                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildListDelegationsCard(listDelegationsResponse.Delegations),
                    cancellationToken);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.ListBotUsersResponse listUsersResponse)
            {
                if (listUsersResponse.Users.Count == 0)
                {
                    await ReplaceLoadingActivityAsync(
                        turnContext,
                        loadingActivityId,
                        TeamsCardBuilder.BuildEmptyCard(),
                        cancellationToken);
                    return;
                }

                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildBotUsersCard(listUsersResponse.Users),
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with bot-users card ({Count})",
                    listUsersResponse.Users.Count);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.ManageBotUserResponse manageUserResponse)
            {
                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildManageBotUserCard(manageUserResponse.User),
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with manage-bot-user card for {SapUserId}",
                    manageUserResponse.User.SapUserId);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.GetOverdueOrdersResponse overdueResponse)
            {
                if (overdueResponse.Orders.Count == 0)
                {
                    await ReplaceLoadingActivityAsync(
                        turnContext,
                        loadingActivityId,
                        TeamsCardBuilder.BuildEmptyCard(),
                        cancellationToken);
                    return;
                }

                var overdueCard = TeamsCardBuilder.BuildOverdueOrdersCard(new
                {
                    count = overdueResponse.Orders.Count,
                    orders = overdueResponse.Orders.Select(o => new
                    {
                        soNumber = o.SoNumber,
                        customerName = o.CustomerName,
                        daysPastDue = o.DaysPastDue,
                        formattedValue = $"{o.NetValue:N0} {o.Currency}",
                        scheduledDeliveryDate = o.ScheduledDeliveryDate.ToString("dd MMM yyyy"),
                        salesOrg = o.SalesOrg
                    }).ToList()
                });

                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    overdueCard,
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with overdue orders card ({Count})", overdueResponse.Orders.Count);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.ConfirmRevokeDelegationResponse confirmRevoke)
            {
                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildConfirmRevokeDelegationCard(
                        confirmRevoke.DelegateUser,
                        confirmRevoke.DelegationId),
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with confirm-revoke-delegation card for user {DelegateUser}",
                    confirmRevoke.DelegateUser);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.ConfirmDelegateApprovalResponse confirmDelegate)
            {
                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildConfirmDelegateApprovalCard(
                        confirmDelegate.DelegateUser,
                        confirmDelegate.ValidFromRaw,
                        confirmDelegate.ValidToRaw,
                        confirmDelegate.ValidFrom,
                        confirmDelegate.ValidTo,
                        confirmDelegate.Reason,
                        confirmDelegate.MaxAmountRaw,
                        confirmDelegate.MaxAmount,
                        confirmDelegate.Currency),
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with confirm-delegate card for user {DelegateUser}",
                    confirmDelegate.DelegateUser);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.ConfirmRequestReleaseResponse confirmRelease)
            {
                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildConfirmRequestReleaseCard(
                        confirmRelease.SoNumber,
                        confirmRelease.Comment),
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with confirm-request-release card for SO {SoNumber}",
                    confirmRelease.SoNumber);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.ConfirmForceCancelResponse confirmForceCancel)
            {
                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildConfirmForceCancelCard(
                        confirmForceCancel.SoNumber,
                        confirmForceCancel.Reason),
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with confirm-force-cancel card for SO {SoNumber}",
                    confirmForceCancel.SoNumber);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.ConfirmCancelOrderResponse confirmCancel)
            {
                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildConfirmCancelCard(
                        confirmCancel.SoNumber,
                        confirmCancel.Reason),
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with confirm-cancel card for SO {SoNumber}",
                    confirmCancel.SoNumber);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.ConfirmCreateOrderResponse confirmCreate)
            {
                // AI path: show Step 1 with the cascading SalesOrg dropdown.
                // When the AI provides a pre-selected SalesOrg, load all three levels
                // so the user can review and navigate. Otherwise start fresh.
                IReadOnlyList<SapSalesOrg> salesOrgs;
                IReadOnlyList<SapDistChannel> distChannels = [];
                IReadOnlyList<SapDivision> divisions = [];
                string? selOrg = null, selChan = null, selDiv = null;

                try
                {
                    salesOrgs = await _sap.GetSalesOrgListAsync(cancellationToken);
                }
                catch
                {
                    salesOrgs = Array.Empty<SapSalesOrg>();
                }

                if (!string.IsNullOrWhiteSpace(confirmCreate.SalesAreaKey)
                    && SapSalesArea.TryParseKey(confirmCreate.SalesAreaKey, out var aiOrg, out var aiChan, out var aiDiv))
                {
                    selOrg = aiOrg;
                    selChan = aiChan;
                    selDiv = aiDiv;
                    try { distChannels = await _sap.GetDistChannelListAsync(aiOrg, cancellationToken); } catch { }
                    try { divisions = await _sap.GetDivisionListAsync(aiOrg, aiChan, cancellationToken); } catch { }
                }

                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildCreateOrderStep1Card(
                        salesOrgs, selOrg, distChannels, selChan, divisions, selDiv),
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with confirm-create card for customer {Customer} ({LineCount} lines)",
                    confirmCreate.Customer,
                    confirmCreate.Lines.Count);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.ConfirmUpdateReferenceResponse confirmUpdateRef)
            {
                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildConfirmUpdateReferenceCard(
                        confirmUpdateRef.SoNumber,
                        confirmUpdateRef.CurrentReference,
                        confirmUpdateRef.NewReference),
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with confirm-update-reference card for SO {SoNumber}",
                    confirmUpdateRef.SoNumber);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.ConfirmEditOrderResponse confirmEdit)
            {
                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildConfirmEditOrderCard(
                        confirmEdit.SoNumber,
                        confirmEdit.CurrentReference,
                        confirmEdit.NewReference,
                        confirmEdit.CurrentReqDate,
                        confirmEdit.NewReqDate,
                        confirmEdit.LineOp,
                        confirmEdit.ItemNumber,
                        confirmEdit.Material,
                        confirmEdit.Qty,
                        confirmEdit.Plant,
                        confirmEdit.Unit,
                        confirmEdit.LinesSummary),
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with confirm-edit-order card for SO {SoNumber}",
                    confirmEdit.SoNumber);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.ConfirmForceReleaseResponse confirmForceRelease)
            {
                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildConfirmForceReleaseCard(
                        confirmForceRelease.SoNumber,
                        confirmForceRelease.Reason),
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with confirm-force-release card for SO {SoNumber}",
                    confirmForceRelease.SoNumber);
                return;
            }

            if (result.Payload is AISO.AiOrchestration.Functions.ConfirmForwardResponse confirmForward)
            {
                var recipientChoices = await _userMappingService.GetForwardRecipientChoicesAsync(
                    cancellationToken,
                    excludeSapUserId: sapUsername,
                    salesOrgFromOrder: confirmForward.SalesOrg);

                var senderDisplayName = await _userMappingService.GetDisplayNameAsync(teamsUserId, cancellationToken);
                var senderName = !string.IsNullOrWhiteSpace(senderDisplayName)
                    ? senderDisplayName
                    : turnContext.Activity.From?.Name ?? "Unknown user";

                if (!string.IsNullOrWhiteSpace(sapUsername)
                    && !string.Equals(senderName, sapUsername, StringComparison.OrdinalIgnoreCase))
                {
                    senderName = $"{senderName} ({sapUsername})";
                }

                await ReplaceLoadingActivityAsync(
                    turnContext,
                    loadingActivityId,
                    TeamsCardBuilder.BuildConfirmForwardCard(
                        confirmForward.SoNumber,
                        recipientChoices,
                        senderName,
                        confirmForward.SuggestedRecipient),
                    cancellationToken);

                _logger.LogInformation(
                    "Bot replied with confirm-forward card for SO {SoNumber}",
                    confirmForward.SoNumber);
                return;
            }

            if (result.Payload is not null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(result.Payload);
                using var doc = System.Text.Json.JsonDocument.Parse(json);

                if (TeamsCardBuilder.TryBuildWorkflowSuccessCard(doc.RootElement, dispatch.FunctionName, out var workflowCard))
                {
                    await ReplaceLoadingActivityAsync(
                        turnContext,
                        loadingActivityId,
                        workflowCard,
                        cancellationToken);

                    _logger.LogInformation(
                        "Bot replied with workflow success card for {Function}", dispatch.FunctionName);
                    return;
                }

                var message = doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && doc.RootElement.TryGetProperty("message", out var msg)
                    ? msg.GetString()
                    : $"✅ Function {dispatch.FunctionName} executed successfully.";

                await ReplaceLoadingActivityAsync(turnContext, loadingActivityId, message, cancellationToken);

                _logger.LogInformation(
                    "Bot replied with action result for {Function}", dispatch.FunctionName);
                return;
            }

            await ReplaceLoadingActivityAsync(
                turnContext,
                loadingActivityId,
                $"Function {dispatch.FunctionName} executed (no result).",
                cancellationToken);
        }
    }

    protected override async Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received Invoke Activity with Name: {InvokeName}", turnContext.Activity.Name);

        // SsoDialog no longer uses OAuthPrompt, so we don't need to handle
        // signin/verifyState or signin/tokenExchange here anymore.
        return await base.OnInvokeActivityAsync(turnContext, cancellationToken);
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
                    MessageFactory.Attachment(
                        TeamsCardBuilder.BuildWelcomeCard(member.Name ?? "there")),
                    cancellationToken);
            }
        }
    }

    private async Task<bool> EnsureLifecycleActionAllowedAsync(
        ITurnContext turnContext,
        string salesOrderId,
        string actionLabel,
        CancellationToken cancellationToken,
        bool blockIfPendingApproval = false,
        bool blockIfNotOwner = false,
        string? currentSapUser = null)
    {
        try
        {
            var order = await _sap.GetSalesOrderByIdAsync(salesOrderId, cancellationToken);
            if (order is null)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                        "NOT_FOUND",
                        $"Sales order {salesOrderId} was not found.")),
                    cancellationToken);
                return false;
            }

            if (order.HasInvalidMaterial)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                        "VALIDATION",
                        SalesOrderWorkflow.BuildInvalidMaterialBlockedMessage(actionLabel))),
                    cancellationToken);
                return false;
            }

            if (SalesOrderWorkflow.BlocksReleaseRejectForward(order.Status)
                && !string.Equals(actionLabel, "Reject", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(actionLabel, "Cancel", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(actionLabel, "Update reference", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(actionLabel, "Edit", StringComparison.OrdinalIgnoreCase))
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                        "VALIDATION",
                        SalesOrderWorkflow.BuildBlockedMessage(order.Status, actionLabel))),
                    cancellationToken);
                return false;
            }

            if ((string.Equals(actionLabel, "Reject", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(actionLabel, "Cancel", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(actionLabel, "Update reference", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(actionLabel, "Edit", StringComparison.OrdinalIgnoreCase))
                && SalesOrderWorkflow.BlocksReject(order.Status))
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                        "VALIDATION",
                        SalesOrderWorkflow.BuildBlockedMessage(order.Status, actionLabel))),
                    cancellationToken);
                return false;
            }

            if (blockIfPendingApproval)
            {
                var pending = await _approvals.GetPendingBySoNumberAsync(order.SoNumber, cancellationToken);
                if (pending is not null)
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                            "VALIDATION",
                            SalesOrderWorkflow.BuildPendingApprovalBlockedMessage(
                                actionLabel,
                                pending.RequestedBySapUser))),
                        cancellationToken);
                    return false;
                }
            }

            if (blockIfNotOwner
                && !SalesOrderWorkflow.IsCurrentOwner(order.OwnerSapUser, currentSapUser)
                && !string.IsNullOrWhiteSpace(order.OwnerSapUser))
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                        "VALIDATION",
                        SalesOrderWorkflow.BuildNotOwnerBlockedMessage(actionLabel, order.OwnerSapUser))),
                    cancellationToken);
                return false;
            }

            return true;
        }
        catch (SapODataException sapEx)
        {
            _logger.LogError(sapEx, "SAP error checking order {OrderId} before {Action}", salesOrderId, actionLabel);
            await turnContext.SendActivityAsync(
                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("SAP_ERROR", sapEx.Message)),
                cancellationToken);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking order {OrderId} before {Action}", salesOrderId, actionLabel);
            await turnContext.SendActivityAsync(
                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard("ACTION_FAILED", ex.Message)),
                cancellationToken);
            return false;
        }
    }

    private static string DeriveStatus(DispatchResult d)
    {
        if (!d.Handled) return "Unrecognized";
        if (d.Denied) return "Denied";
        if (d.Result is null) return "Failed";
        return d.Result.Success ? "Success" : "Failed";
    }

    private static bool IsOrderDetailFunction(string? functionName) =>
        string.Equals(functionName, "CheckOrderStatus", StringComparison.OrdinalIgnoreCase)
        || string.Equals(functionName, "GetOrderDetail", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// EN/VI phrases that should open the Help Adaptive Card (not the AI function dump).
    /// </summary>
    private static bool IsHelpIntent(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var t = message.Trim().ToLowerInvariant();
        return t is "help" or "hướng dẫn" or "huong dan" or "trợ giúp" or "tro giup"
            or "hướng dẫn sử dụng" or "huong dan su dung" or "guide" or "commands";
    }

    private static async Task ReplaceLoadingActivityAsync(ITurnContext turnContext, string? loadingActivityId, Attachment? attachment, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(loadingActivityId) || attachment is null)
        {
            if (attachment is not null)
            {
                await turnContext.SendActivityAsync(MessageFactory.Attachment(attachment), cancellationToken);
            }
            return;
        }

        var replacement = new Microsoft.Bot.Schema.Activity
        {
            Type = ActivityTypes.Message,
            Id = loadingActivityId,
            Conversation = turnContext.Activity.Conversation,
            ChannelId = turnContext.Activity.ChannelId,
            ServiceUrl = turnContext.Activity.ServiceUrl,
            From = turnContext.Activity.Recipient,
            Recipient = turnContext.Activity.From,
            Attachments = new List<Attachment> { attachment },
            Text = string.Empty
        };

        await turnContext.UpdateActivityAsync(replacement, cancellationToken);
    }

    private static async Task ReplaceLoadingActivityAsync(ITurnContext turnContext, string? loadingActivityId, string? text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(loadingActivityId) || string.IsNullOrWhiteSpace(text))
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                await turnContext.SendActivityAsync(text, cancellationToken: cancellationToken);
            }
            return;
        }

        var replacement = new Microsoft.Bot.Schema.Activity
        {
            Type = ActivityTypes.Message,
            Id = loadingActivityId,
            Conversation = turnContext.Activity.Conversation,
            ChannelId = turnContext.Activity.ChannelId,
            ServiceUrl = turnContext.Activity.ServiceUrl,
            From = turnContext.Activity.Recipient,
            Recipient = turnContext.Activity.From,
            Text = text
        };

        await turnContext.UpdateActivityAsync(replacement, cancellationToken);
    }

    private async Task<bool> TryHandleOrderDetailRequest(string message, ITurnContext turnContext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var lowered = message.ToLowerInvariant();
        var isDetailRequest =
            lowered.Contains("detail")
            || lowered.Contains("chi tiết")
            || lowered.Contains("xem chi tiết")
            || lowered.Contains("show detail")
            || lowered.Contains("view order")
            || lowered.Contains("xem đơn")
            || lowered.Contains("xem order");
        var mentionsOrder =
            lowered.Contains("order")
            || lowered.Contains("đơn hàng")
            || lowered.Contains("đơn")
            || lowered.Contains("so")
            || lowered.Contains("sales order");

        if (!isDetailRequest || !mentionsOrder)
        {
            return false;
        }

        var match = System.Text.RegularExpressions.Regex.Match(message, @"(?:order|so|sales order|đơn hàng|đơn)\s*(?:no\.?|number|#)?\s*([A-Za-z0-9\-\/]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var orderId = match.Success ? match.Groups[1].Value : "UNKNOWN";

        var order = await _sap.GetSalesOrderByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            await turnContext.SendActivityAsync(
                MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                    "NOT_FOUND",
                    $"Sales order {orderId} was not found.")),
                cancellationToken);
            return true;
        }

        var teamsUserId = turnContext.Activity.From?.Id ?? "anonymous";
        var roleForDetail = await _userMappingService.GetRoleAsync(teamsUserId, cancellationToken);
        var linkedSapForDetail = await _userMappingService.GetSapUsernameAsync(teamsUserId, cancellationToken);
        await turnContext.SendActivityAsync(
            MessageFactory.Attachment(await BuildSalesOrderDetailAttachmentAsync(
                order,
                roleForDetail,
                linkedSapForDetail,
                cancellationToken)),
            cancellationToken);
        return true;
    }

    private async Task<Attachment> BuildSalesOrderDetailAttachmentAsync(
        Domain.SalesOrders.SalesOrder order,
        UserRole role,
        string? currentSapUser,
        CancellationToken cancellationToken)
    {
        var latest = await _approvals.GetLatestBySoNumberAsync(order.SoNumber, cancellationToken);
        var pending = latest is { Status: ApprovalStatus.Pending }
            ? latest
            : null;

        return TeamsCardBuilder.BuildSalesOrderDetailCard(
            order,
            role,
            hasPendingApproval: pending is not null,
            pendingRequestedBySapUser: pending?.RequestedBySapUser ?? latest?.RequestedBySapUser,
            currentSapUser: currentSapUser,
            pendingComment: pending?.Comment,
            approval: latest);
    }

    private async Task<IReadOnlyDictionary<string, OrderApprovalRequest?>> GetLatestApprovalsBySoAsync(
        IReadOnlyList<Domain.SalesOrders.SalesOrder> orders,
        CancellationToken cancellationToken)
    {
        var pairs = await Task.WhenAll(orders.Select(async order =>
        {
            var latest = await _approvals.GetLatestBySoNumberAsync(order.SoNumber, cancellationToken);
            return (order.SoNumber, latest);
        }));

        return pairs.ToDictionary(
            p => p.SoNumber,
            p => p.latest,
            StringComparer.OrdinalIgnoreCase);
    }
}
