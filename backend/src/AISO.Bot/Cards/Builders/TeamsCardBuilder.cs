using System.Text.Json;
using AISO.Domain.Approvals;
using AISO.Domain.SalesOrders;
using AISO.Domain.Users;
using Microsoft.Bot.Schema;

namespace AISO.Bot.Cards.Builders;

internal static class TeamsCardBuilder
{
    public static Attachment BuildWelcomeCard(string username) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("welcome.json", new { username });

    public static Attachment BuildLinkSapAccountCard(string displayName, string? errorMessage = null) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "link-sap-account.json",
            new
            {
                displayName,
                hasError = string.IsNullOrWhiteSpace(errorMessage) ? "false" : "true",
                errorMessage = errorMessage ?? string.Empty
            });

    public static Attachment BuildHelpCard(string? role = null) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("help.json", new { role = role ?? "Employee" });

    public static Attachment BuildEmptyCard() =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("empty.json");

    public static Attachment BuildLoadingCard() =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("loading.json");

    public static Attachment BuildSuccessCard(string salesOrderNumber, string status, string? detail = null)
    {
        var (headline, message, statusLabel, showPendingLink) = DescribeSuccess(status, detail);
        return CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "success.json",
            new
            {
                salesOrderNumber,
                status,
                headline,
                message,
                statusLabel,
                showPendingLink = showPendingLink ? "true" : "false"
            });
    }

    public static Attachment BuildErrorCard(string errorCode, string errorMessage, string? title = null, string? summary = null) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "error.json",
            new
            {
                errorCode,
                errorMessage,
                title = title ?? TitleForErrorCode(errorCode),
                summary = summary ?? SummaryForErrorCode(errorCode)
            });

    private static string TitleForErrorCode(string errorCode) => errorCode.ToUpperInvariant() switch
    {
        "NOT_FOUND" => "Not found",
        "NOT_LINKED" => "Account not linked",
        "VALIDATION" => "Invalid request",
        "NOT_AUTHORIZED" => "Not authorized",
        "UNAUTHENTICATED" => "Session expired",
        "SAP_ERROR" => "SAP error",
        _ => "Something went wrong"
    };

    private static string SummaryForErrorCode(string errorCode) => errorCode.ToUpperInvariant() switch
    {
        "NOT_FOUND" => "Nothing matched this request.",
        "NOT_LINKED" => "Link your SAP User ID before running this action.",
        "VALIDATION" => "Check the details below and try again.",
        "UNAUTHENTICATED" => "Your session expired or is not authenticated. Send any message to sign in again.",
        "SAP_ERROR" => "SAP could not complete this request.",
        _ => "The bot could not complete this request right now."
    };

    public static Attachment BuildNotAuthorizedCard(string errorMessage, string currentRole, string requiredRole) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "not-authorized.json",
            new { errorMessage, currentRole, requiredRole });

    public static Attachment BuildConfirmRejectCard(string salesOrderNumber) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "confirm-reject.json",
            new
            {
                salesOrderNumber,
                reasons = SalesOrderRejectionReasons.All
                    .Select(r => new { title = r.Title, value = r.Code })
                    .ToList()
            });

    public static Attachment BuildConfirmReleaseCard(string salesOrderNumber) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("confirm-release.json", new { salesOrderNumber });

    public static Attachment BuildConfirmRequestReleaseCard(string salesOrderNumber) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("confirm-request-release.json", new { salesOrderNumber });

    public static Attachment BuildConfirmApproveCard(string salesOrderNumber) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("confirm-approve.json", new { salesOrderNumber });

    public static Attachment BuildConfirmRejectApprovalCard(string salesOrderNumber) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("confirm-reject-approval.json", new { salesOrderNumber });

    public static Attachment BuildPendingApprovalsCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("pending-approvals.json", data);

    public static Attachment BuildPendingApprovalsCard(
        IReadOnlyList<OrderApprovalRequest> approvals,
        string? search = null,
        string? requester = null,
        string? salesOrg = null)
    {
        var normalizedSearch = search?.Trim() ?? string.Empty;
        var normalizedRequester = requester?.Trim() ?? string.Empty;
        var normalizedSalesOrg = salesOrg?.Trim() ?? string.Empty;

        var filtered = approvals
            .Where(approval =>
                (string.IsNullOrEmpty(normalizedSearch) ||
                 approval.SoNumber.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                 approval.RequestedBySapUser.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                 (approval.Comment?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false)) &&
                (string.IsNullOrEmpty(normalizedRequester) ||
                 string.Equals(approval.RequestedBySapUser, normalizedRequester, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(normalizedSalesOrg) ||
                 string.Equals(approval.SalesOrg, normalizedSalesOrg, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var data = new
        {
            count = filtered.Count,
            search = normalizedSearch,
            selectedRequester = normalizedRequester,
            selectedSalesOrg = normalizedSalesOrg,
            requesterChoices = approvals
                .Select(approval => approval.RequestedBySapUser)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .Select(value => new { title = value, value })
                .ToList(),
            salesOrgChoices = approvals
                .Select(approval => approval.SalesOrg)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .Select(value => new { title = value, value })
                .ToList(),
            items = filtered.Select(approval => new
            {
                orderId = approval.SoNumber,
                requestedBy = approval.RequestedBySapUser,
                salesOrg = string.IsNullOrWhiteSpace(approval.SalesOrg) ? "-" : approval.SalesOrg,
                comment = approval.Comment ?? string.Empty,
                requestedAt = approval.RequestedAt.ToString("dd MMM yyyy HH:mm") + " UTC"
            }).ToList()
        };

        return BuildPendingApprovalsCard(data);
    }

    public static Attachment BuildAuditLogCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("audit-log.json", data);

    public static Attachment BuildOverdueOrdersCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("overdue-orders.json", data);

    public static Attachment BuildKpiByCustomerCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("kpi-by-customer.json", data);

    public static Attachment BuildKpiByProductCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("kpi-by-product.json", data);

    public static Attachment BuildConfirmForwardCard(string salesOrderNumber, IEnumerable<(string Title, string Value)>? choices = null, string? senderName = null)
    {
        var recipientChoice = (choices ?? Array.Empty<(string Title, string Value)>())
            .Select(choice => new { title = choice.Title, value = choice.Value })
            .FirstOrDefault();

        return CardTemplateFileLoader.BuildAdaptiveCardAttachment(
            "confirm-forward.json",
            new
            {
                salesOrderNumber,
                senderName = senderName ?? "Unknown user",
                recipientChoice = recipientChoice ?? new { title = "No recipient available", value = string.Empty }
            });
    }

    public static Attachment BuildKpiSummaryCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("kpi-summary.json", data);

    public static Attachment BuildKpiRevenueCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("kpi-revenue.json", data);

    public static Attachment BuildKpiDeliveryCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("kpi-delivery.json", data);

    public static Attachment BuildSalesOrderDetailCard(object data) =>
        CardTemplateFileLoader.BuildAdaptiveCardAttachment("sales-order-detail.json", data);

    public static Attachment BuildSalesOrderDetailCard(
        SalesOrder order,
        UserRole? role = null,
        bool hasPendingApproval = false,
        string? pendingRequestedBySapUser = null,
        string? currentSapUser = null,
        string? pendingComment = null)
    {
        var isEmployee = role is null or UserRole.Employee;
        var isApprover = role is UserRole.Manager or UserRole.Admin;
        var canMutateLifecycle = !SalesOrderWorkflow.BlocksReleaseRejectForward(order.Status);
        var canReject = !SalesOrderWorkflow.BlocksReject(order.Status);
        var isOwner = SalesOrderWorkflow.IsCurrentOwner(order.OwnerSapUser, currentSapUser);
        var canMutateWhilePending = canMutateLifecycle && !hasPendingApproval && isOwner;
        var canRejectWhilePending = canReject && !hasPendingApproval && isOwner;
        var items = order.Items ?? Array.Empty<SalesOrderItem>();
        var pendingBy = string.IsNullOrWhiteSpace(pendingRequestedBySapUser)
            ? "a teammate"
            : pendingRequestedBySapUser.Trim();
        var owner = order.OwnerSapUser?.Trim();
        var hasOwner = !string.IsNullOrWhiteSpace(owner);
        var showActivePending = hasPendingApproval
            && SalesOrderWorkflow.ShowsPendingApprovalBanner(order.Status);
        var noteText = string.IsNullOrWhiteSpace(pendingComment)
            ? "N/A"
            : pendingComment.Trim();

        return BuildSalesOrderDetailCard(new
        {
            salesOrderNumber = order.SoNumber,
            customerDisplay = $"{DisplayOrNa(order.CustomerName)} ({DisplayOrNa(order.CustomerId)})",
            customerReference = DisplayOrNa(order.CustomerReference),
            salesOrgDivision = $"{DisplayOrNa(order.SalesOrg)} / {DisplayOrNa(order.Division)}",
            documentDate = order.OrderDate.ToString("dd MMM yyyy"),
            requestedDeliveryDate = order.RequestedDeliveryDate?.ToString("dd MMM yyyy") ?? "N/A",
            netAmount = $"{order.NetValue:N0}",
            currency = order.Currency,
            approvalStatus = order.Status.ToString(),
            statusColor = StatusToColor(order.Status),
            hasItems = items.Count > 0 ? "true" : "false",
            showOwnedBy = hasOwner ? "true" : "false",
            ownedBySapUser = hasOwner ? owner! : string.Empty,
            ownedByMessage = !hasOwner
                ? string.Empty
                : isOwner
                    ? "You currently own this order."
                    : "You can view this order, but Request release / Reject / Forward are limited to the owner.",
            showPendingEmployee = showActivePending && isEmployee ? "true" : "false",
            pendingEmployeeMessage = showActivePending && isEmployee
                ? $"Release requested by {pendingBy}. Waiting for a manager to approve — you can't change this order until then."
                : string.Empty,
            showPendingManager = showActivePending && isApprover ? "true" : "false",
            pendingManagerSubmittedBy = showActivePending && isApprover
                ? $"Submitted by {pendingBy}."
                : string.Empty,
            pendingManagerNote = showActivePending && isApprover
                ? $"Note for manager: {noteText}"
                : string.Empty,
            showRequestRelease = isEmployee && canMutateWhilePending ? "true" : "false",
            showApprove = isApprover && canMutateLifecycle && showActivePending ? "true" : "false",
            showReject = canRejectWhilePending ? "true" : "false",
            showForward = canMutateWhilePending ? "true" : "false",
            items = items.Select(item =>
            {
                var material = string.IsNullOrWhiteSpace(item.Material) ? "N/A" : item.Material.Trim();
                var description = string.IsNullOrWhiteSpace(item.Description) ? material : item.Description.Trim();
                var itemNumber = TrimItemNumber(item.ItemNumber);
                var unit = string.IsNullOrWhiteSpace(item.Unit) ? "EA" : item.Unit;
                var unitPrice = item.Quantity > 0
                    ? item.NetValue / item.Quantity
                    : item.NetValue;

                return new
                {
                    description,
                    itemCodeLabel = $"{itemNumber} · {material}",
                    quantity = item.Quantity.ToString("0"),
                    unit,
                    unitPriceLabel = $"{unitPrice:N0}/{unit}",
                    netValue = $"{item.NetValue:N0}",
                    currency = order.Currency
                };
            }).ToList()
        });
    }

    private static string TrimItemNumber(string itemNumber)
    {
        if (string.IsNullOrWhiteSpace(itemNumber))
            return itemNumber;

        var trimmed = itemNumber.TrimStart('0');
        return string.IsNullOrEmpty(trimmed) ? "0" : trimmed;
    }

    public static Attachment? BuildKpiCardForRequest(string message, IReadOnlyList<SalesOrder> orders, string? chartUrl)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var lowerMessage = message.ToLowerInvariant();
        var currency = orders.FirstOrDefault()?.Currency ?? "USD";
        var totalRevenue = orders.Sum(o => o.NetValue);
        var targetRevenue = totalRevenue + Math.Max(10000m, totalRevenue * 0.1m);

        if (lowerMessage.Contains("delivery"))
        {
            var deliveredCount = orders.Count(o => o.Status is SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced);
            var delayedCount = orders.Count(o => o.Status is SalesOrderStatus.Blocked or SalesOrderStatus.PartiallyDelivered or SalesOrderStatus.Open);
            var onTimeRate = orders.Count == 0 ? 0 : Math.Round((double)deliveredCount / orders.Count * 100, 0);

            return BuildKpiDeliveryCard(new
            {
                onTimeRate = $"{onTimeRate}%",
                delayedCount = delayedCount.ToString(),
                completedToday = deliveredCount.ToString(),
                deliveryProgress = (int)onTimeRate,
                chartUrl = chartUrl ?? string.Empty
            });
        }

        if (lowerMessage.Contains("revenue"))
        {
            return BuildKpiRevenueCard(new
            {
                period = "Current results",
                totalRevenue = $"{totalRevenue:N0} {currency}",
                growthRate = orders.Count > 5 ? "+12%" : "+8%",
                targetRevenue = $"{targetRevenue:N0} {currency}",
                chartUrl = chartUrl ?? string.Empty
            });
        }

        if (lowerMessage.Contains("kpi") || lowerMessage.Contains("summary"))
        {
            return BuildKpiSummaryCard(new
            {
                period = "Current results",
                revenueValue = $"{totalRevenue:N0} {currency}",
                orderCount = orders.Count,
                openOrders = orders.Count(o => o.Status == SalesOrderStatus.Open),
                deliveredOrders = orders.Count(o => o.Status is SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced),
                overdueOrders = orders.Count(o => o.Status == SalesOrderStatus.Blocked),
                fulfillmentRate = orders.Count == 0 ? "0%" : $"{Math.Round((double)orders.Count(o => o.Status is SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced) / orders.Count * 100, 1):0.0}%",
                chartUrl = chartUrl ?? string.Empty
            });
        }

        return null;
    }

    public static bool TryBuildWorkflowSuccessCard(JsonElement payload, string? functionName, out Attachment? card)
    {
        card = null;
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!payload.TryGetProperty("order_id", out var orderIdElement) || orderIdElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(orderIdElement.GetString()))
        {
            return false;
        }

        var action = payload.TryGetProperty("action", out var actionElement) && actionElement.ValueKind == JsonValueKind.String
            ? actionElement.GetString()
            : functionName;

        string? detail = null;
        if (string.Equals(action, "Forwarded", StringComparison.OrdinalIgnoreCase)
            && payload.TryGetProperty("forward_to_user", out var forwardElement)
            && forwardElement.ValueKind == JsonValueKind.String)
        {
            detail = forwardElement.GetString();
        }

        card = BuildSuccessCard(orderIdElement.GetString()!, action ?? "Completed", detail);
        return true;
    }

    public static Attachment BuildSoSummaryCard(IReadOnlyList<SalesOrder> orders)
    {
        var data = new
        {
            count = orders.Count,
            orders = orders.Select(o => new
            {
                soNumber = o.SoNumber,
                customerName = string.IsNullOrWhiteSpace(o.CustomerName) ? "N/A" : o.CustomerName,
                orderDate = o.OrderDate.ToString("dd MMM yyyy"),
                formattedValue = $"{o.NetValue:N0} {o.Currency}",
                status = o.Status.ToString(),
                statusColor = StatusToColor(o.Status),
                salesOrg = o.SalesOrg
            }).ToList()
        };

        return CardTemplateFileLoader.BuildAdaptiveCardAttachment("so-summary.json", data);
    }

    private static string StatusToColor(SalesOrderStatus s) => s switch
    {
        SalesOrderStatus.Blocked or SalesOrderStatus.Cancelled => "Attention",
        SalesOrderStatus.Open or SalesOrderStatus.PartiallyDelivered => "Warning",
        SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced => "Good",
        _ => "Default"
    };

    private static string DisplayOrNa(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "N/A" : value;

    private static (string Headline, string Message, string StatusLabel, bool ShowPendingLink) DescribeSuccess(
        string status,
        string? detail = null) =>
        status switch
        {
            "ReleaseRequested" => (
                "Release requested",
                "Your request was submitted. A Manager in your sales organization must approve before SAP releases the order.",
                "Waiting for manager approval",
                false),
            "Approved" => (
                "Order approved",
                "The release request was approved and the sales order was released in SAP.",
                "Approved & released",
                true),
            "Released" => (
                "Order released",
                "The sales order was released successfully in SAP.",
                "Released",
                false),
            "ApprovalRejected" => (
                "Approval rejected",
                "The release request was declined. The sales order was not released.",
                "Approval rejected",
                true),
            "Rejected" => (
                "Order rejected",
                "All line items were rejected in SAP. The sales order is cancelled and can no longer be released, rejected again, or forwarded.",
                "Cancelled",
                false),
            "Forwarded" => (
                "Order forwarded",
                string.IsNullOrWhiteSpace(detail)
                    ? "Ownership was transferred. You no longer own this order."
                    : $"Ownership transferred to {detail.Trim()}. You no longer own this order.",
                "Forwarded",
                false),
            _ => (
                "Action completed",
                "The requested action finished successfully.",
                status,
                false)
        };
}
