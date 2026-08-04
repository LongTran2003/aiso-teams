
using System.Diagnostics;
using AISO.AiOrchestration;
using AISO.Bot.Cards;
using AISO.Bot.Cards.Builders;
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
        IBotUserAdminService botUserAdmin)
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

        // When an Adaptive Card button (Action.Submit with msteams.type=messageBack) is clicked,
        // Teams sends BOTH Activity.Text (= button title, e.g. "view order 129998")
        // AND Activity.Value (= the data payload, e.g. { action: "view_details" }).
        // We MUST check Value first so the structured command wins over the display title.
        if (turnContext.Activity.Value != null)
        {
            try
            {
                var valueObj = Newtonsoft.Json.Linq.JObject.FromObject(turnContext.Activity.Value);

                // For messageBack cards, Teams might not reliably populate Activity.Text. 
                // We extract msteams.text directly from Value if present.
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
                            var updated = await _botUserAdmin.UpdateAccessAsync(
                                sapUserId,
                                newRole,
                                newSalesOrg,
                                cancellationToken);

                            await _audit.LogAsync(new AuditEntry
                            {
                                TeamsUserId = teamsUserId,
                                ConversationId = conversationId,
                                Action = "ManageBotUser",
                                ParametersJson = JsonConvert.SerializeObject(new
                                {
                                    sap_user_id = updated.SapUserId,
                                    role = updated.Role.ToString(),
                                    sales_org = updated.SalesOrg
                                }),
                                ResultStatus = "Success"
                            }, cancellationToken);

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

                                var updated = await _sap.ReleaseOrderAsync(pending.SoNumber, linkedSapUsername, cancellationToken);
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

                            var updatedOrder = await _sap.ReleaseOrderAsync(salesOrderId, linkedSapUsername, cancellationToken);
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

                            var updated = await _sap.ReleaseOrderAsync(pending.SoNumber, linkedSapUsername, cancellationToken);
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
            catch { /* Ignore parsing errors, userMessage stays empty */ }
        }

        var normalizedMessage = userMessage.Trim();

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

                var card = TeamsCardBuilder.BuildSoSummaryCard(orders, getOrdersResponse.Title);
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

                var card = TeamsCardBuilder.BuildSoSummaryCard(ordersList);
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

            // Workflow action results (Release, Reject, Forward) — show a success card when applicable
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
                && !string.Equals(actionLabel, "Reject", StringComparison.OrdinalIgnoreCase))
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Attachment(TeamsCardBuilder.BuildErrorCard(
                        "VALIDATION",
                        SalesOrderWorkflow.BuildBlockedMessage(order.Status, actionLabel))),
                    cancellationToken);
                return false;
            }

            if (string.Equals(actionLabel, "Reject", StringComparison.OrdinalIgnoreCase)
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
}
