using System.Text.Json;
using System.Text.RegularExpressions;
using AISO.Domain.SalesOrders;
using AISO.Domain.Users;

namespace AISO.AiOrchestration.Stub;

/// <summary>
/// Placeholder dispatcher using simple keyword matching.
/// Replaced by AI microservice dispatcher when AiService:UseKeywordFallback=false.
/// </summary>
public sealed partial class KeywordFunctionDispatcher : IFunctionDispatcher
{
    private readonly IFunctionRegistry _registry;

    public KeywordFunctionDispatcher(IFunctionRegistry registry)
    {
        _registry = registry;
    }

    public async Task<DispatchResult> DispatchAsync(
        string userMessage,
        string requestingSapUser,
        UserRole role,
        CancellationToken ct = default)
    {
        var result = await DispatchInternalAsync(userMessage, requestingSapUser, ct);

        // Role-based access control (Phase B): apply the same gate as the AI dispatcher.
        if (result.Handled && result.FunctionName is { } fn && !RolePolicy.CanExecute(role, fn))
        {
            var requiredRole = RolePolicy.RequiredRole(fn);
            return result with
            {
                Denied = true,
                Result = FunctionResult.Fail(
                    $"You do not have permission to perform this action. " +
                    $"'{fn}' requires the {requiredRole} role, but your role is {role}.")
            };
        }

        return result;
    }

    private async Task<DispatchResult> DispatchInternalAsync(string userMessage, string requestingSapUser, CancellationToken ct = default)
    {
        var text = userMessage.Trim().ToLowerInvariant();

        // Admin: audit log (Help shortcut "view audit log")
        if (text.Contains("audit log") || text.Contains("auditlog") || text.Contains("getauditlog")
            || text.Contains("view audit") || text.Contains("show audit")
            || text.Contains("nhật ký audit") || text.Contains("nhat ky audit"))
        {
            var auditFn = _registry.GetByName("ViewAuditLog");
            if (auditFn is not null)
            {
                var paramsJson = "{}";
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await auditFn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult
                {
                    Handled = true,
                    FunctionName = auditFn.Name,
                    Result = result,
                    ParametersJson = paramsJson
                };
            }
        }

        // Admin: list / manage bot users (before generic "show … order")
        var manageUserMatch = Regex.Match(
            text,
            @"(?:manage\s+user|set\s+role|set\s+sales\s*org)\s+([a-z0-9_-]+)",
            RegexOptions.IgnoreCase);
        if (manageUserMatch.Success)
        {
            var fn = _registry.GetByName("ManageBotUser");
            if (fn is not null)
            {
                var sapId = manageUserMatch.Groups[1].Value.ToUpperInvariant();
                var paramsJson = JsonSerializer.Serialize(new { sap_user_id = sapId });
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult
                {
                    Handled = true,
                    FunctionName = fn.Name,
                    Result = result,
                    ParametersJson = paramsJson
                };
            }
        }

        if (text.Contains("list user") || text.Contains("show user") || text.Contains("bot user")
            || text.Contains("manage users") || text.Contains("danh sách user") || text.Contains("danh sach user")
            || text.Trim() is "manage user")
        {
            var listFn = _registry.GetByName("ListBotUsers");
            if (listFn is not null)
            {
                var paramsJson = "{}";
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await listFn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult
                {
                    Handled = true,
                    FunctionName = listFn.Name,
                    Result = result,
                    ParametersJson = paramsJson
                };
            }
        }

        // "my profile" — before generic "show … order" so a profile request never
        // falls through to GetSalesOrders (defensive: profile text does not contain
        // "order"/"đơn", but ordering keeps intent routing predictable).
        if (IsMyProfileIntent(text))
        {
            var fn = _registry.GetByName("MyProfile");
            if (fn is not null)
            {
                var paramsJson = "{}";
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult
                {
                    Handled = true,
                    FunctionName = fn.Name,
                    Result = result,
                    ParametersJson = paramsJson
                };
            }
        }

        // Manager: pending approvals (EN + VI) — before generic "show … order" / "đơn"
        if (IsPendingApprovalsIntent(text))
        {
            var pendingFn = _registry.GetByName("GetPendingApprovals");
            if (pendingFn is not null)
            {
                var paramsJson = "{}";
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await pendingFn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult
                {
                    Handled = true,
                    FunctionName = pendingFn.Name,
                    Result = result,
                    ParametersJson = paramsJson
                };
            }
        }

        // Pattern 1: Check specific order — "kiểm tra đơn hàng 5001" or "check order 5001" or "show sales order 5001"
        if (text.Contains("kiểm tra") || text.Contains("check") || text.Contains("show"))
        {
            var match = OrderIdPattern().Match(text);
            if (match.Success)
            {
                var orderId = match.Groups[1].Value.PadLeft(10, '0');
                var fn = _registry.GetByName("CheckOrderStatus");
                if (fn is not null)
                {
                    var paramsJson = JsonSerializer.Serialize(new { order_id = orderId });
                    using var doc = JsonDocument.Parse(paramsJson);
                    var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                    return new DispatchResult
                    {
                        Handled = true,
                        FunctionName = fn.Name,
                        Result = result,
                        ParametersJson = paramsJson
                    };
                }
            }
        }

        // Pattern: Create Order — EN + VI (with and without diacritics)
        if ((text.Contains("tạo") && text.Contains("đơn"))
            || (text.Contains("tao") && text.Contains("don"))
            || text.Contains("tạo đơn hàng")
            || text.Contains("tao don hang")
            || text.Contains("tạo đơn bán hàng")
            || text.Contains("tao don ban hang")
            || text.Contains("tạo sales order")
            || text.Contains("tao sales order")
            || text.Contains("tạo so")
            || text.Contains("tao so")
            || text.Contains("lập đơn")
            || text.Contains("lap don")
            || text.Contains("lập đơn hàng")
            || text.Contains("lap don hang")
            || text.Contains("lập đơn bán hàng")
            || text.Contains("lap don ban hang")
            || text.Contains("tạo đơn mới")
            || text.Contains("tao don moi")
            || text.Contains("create order")
            || text.Contains("create so")
            || text.Contains("create sales order")
            || text.Contains("new sales order")
            || text.Contains("new order")
            || text.Contains("place order")
            || text.Contains("make order"))
        {
            var fn = _registry.GetByName("CreateOrder");
            if (fn is not null)
            {
                var custMatch = Regex.Match(
                    text,
                    @"(?:customer|khách(?:\s+hàng)?|cho khách)\s+([a-z0-9_]+)|(uscu_[a-z0-9]+)|(\d{6,10})",
                    RegexOptions.IgnoreCase);
                var matMatch = Regex.Match(
                    text,
                    @"(?:material|mặt hàng|item)\s+([a-z0-9_-]+)",
                    RegexOptions.IgnoreCase);
                var qtyMatch = Regex.Match(
                    text,
                    @"(?:qty|quantity|số lượng)\s+(\d+)|(\d+)\s+(?:pc|pcs|units?)",
                    RegexOptions.IgnoreCase);

                // Only pass customer if explicitly specified in the message.
                // When omitted, CreateOrderFunction pre-fills with the first SAP customer.
                string? customerId = null;
                if (custMatch.Success)
                {
                    customerId = (custMatch.Groups[1].Success ? custMatch.Groups[1].Value
                        : custMatch.Groups[2].Success ? custMatch.Groups[2].Value
                        : custMatch.Groups[3].Value).ToUpperInvariant();
                }

                var material = matMatch.Success ? matMatch.Groups[1].Value.ToUpperInvariant() : null;
                var qty = 1;
                if (qtyMatch.Success)
                {
                    qty = int.Parse(qtyMatch.Groups[1].Success ? qtyMatch.Groups[1].Value : qtyMatch.Groups[2].Value);
                }

                // Build minimal params — omit customer/material when not specified so the
                // CreateOrderFunction shows Step 1 with SAP-loaded dropdowns.
                object paramsObj;
                if (customerId is not null && material is not null)
                    paramsObj = new { customer = customerId, items = new[] { new { material, qty } } };
                else if (customerId is not null)
                    paramsObj = new { customer = customerId };
                else if (material is not null)
                    paramsObj = new { items = new[] { new { material, qty } } };
                else
                    paramsObj = new { };

                var paramsJson = JsonSerializer.Serialize(paramsObj);
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult { Handled = true, FunctionName = fn.Name, Result = result, ParametersJson = paramsJson };
            }
        }

        // Pattern: Update Reference (EN + VI)
        if (IsUpdateReferenceIntent(text))
        {
            var fn = _registry.GetByName("UpdateOrderReference");
            if (fn is not null)
            {
                var orderMatch = OrderIdPattern().Match(text);
                var refMatch = Regex.Match(
                    text,
                    @"(?:thành|to|thanh)\s+['""]?([^'""]+)['""]?",
                    RegexOptions.IgnoreCase);
                if (!refMatch.Success)
                {
                    refMatch = Regex.Match(
                        text,
                        @"reference\s+(?:to\s+)?['""]?([a-z0-9][a-z0-9 _\-/]*)['""]?\s*$",
                        RegexOptions.IgnoreCase);
                }

                var orderId = orderMatch.Success ? orderMatch.Groups[1].Value.PadLeft(10, '0') : "0000000000";
                var newRef = refMatch.Success ? refMatch.Groups[1].Value.Trim().TrimEnd('.', '!', '?') : "Updated Reference";

                var paramsObj = new { order_id = orderId, new_reference = newRef };
                var paramsJson = JsonSerializer.Serialize(paramsObj);
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult { Handled = true, FunctionName = fn.Name, Result = result, ParametersJson = paramsJson };
            }
        }

        // Pattern: Edit order (full header + line) — after update-reference so "cập nhật reference" stays specific
        if (IsEditOrderIntent(text))
        {
            var fn = _registry.GetByName("EditOrder");
            if (fn is not null)
            {
                var orderMatch = OrderIdPattern().Match(text);
                var orderId = orderMatch.Success ? orderMatch.Groups[1].Value.PadLeft(10, '0') : "0000000000";
                var paramsObj = new { order_id = orderId };
                var paramsJson = JsonSerializer.Serialize(paramsObj);
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult { Handled = true, FunctionName = fn.Name, Result = result, ParametersJson = paramsJson };
            }
        }

        // Admin force* must run before generic cancel/reject/release (e.g. "force cancel 13069").
        if (IsForceCancelIntent(text) && (text.Contains("đơn") || text.Contains("order") || OrderIdPattern().IsMatch(text)))
        {
            var fn = _registry.GetByName("ForceCancel");
            if (fn is not null)
            {
                var orderMatch = OrderIdPattern().Match(text);
                var orderId = orderMatch.Success ? orderMatch.Groups[1].Value.PadLeft(10, '0') : "0000000000";
                var reasonMatch = Regex.Match(text, @"reason:\s*(.+)$", RegexOptions.IgnoreCase);
                var reason = reasonMatch.Success
                    ? reasonMatch.Groups[1].Value.Trim()
                    : null;

                object paramsObj = string.IsNullOrWhiteSpace(reason)
                    ? new { order_id = orderId }
                    : new { order_id = orderId, reason };
                var paramsJson = JsonSerializer.Serialize(paramsObj);
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult { Handled = true, FunctionName = fn.Name, Result = result, ParametersJson = paramsJson };
            }
        }

        // Cancel order (before RejectOrder — "cancel order" / "hủy đơn")
        if (IsCancelOrderIntent(text) && (text.Contains("đơn") || text.Contains("order") || OrderIdPattern().IsMatch(text)))
        {
            var fn = _registry.GetByName("CancelOrder");
            if (fn is not null)
            {
                var orderMatch = OrderIdPattern().Match(text);
                var orderId = orderMatch.Success ? orderMatch.Groups[1].Value.PadLeft(10, '0') : "0000000000";
                var reasonMatch = Regex.Match(text, @"reason:\s*(.+)$", RegexOptions.IgnoreCase);
                var reason = reasonMatch.Success
                    ? reasonMatch.Groups[1].Value.Trim()
                    : null;

                object paramsObj = string.IsNullOrWhiteSpace(reason)
                    ? new { order_id = orderId }
                    : new { order_id = orderId, reason };
                var paramsJson = JsonSerializer.Serialize(paramsObj);
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult { Handled = true, FunctionName = fn.Name, Result = result, ParametersJson = paramsJson };
            }
        }

        if (IsForceReleaseIntent(text) && (text.Contains("đơn") || text.Contains("order") || OrderIdPattern().IsMatch(text)))
        {
            var fn = _registry.GetByName("ForceRelease");
            if (fn is not null)
            {
                var orderMatch = OrderIdPattern().Match(text);
                var orderId = orderMatch.Success ? orderMatch.Groups[1].Value.PadLeft(10, '0') : "0000000000";
                var reasonMatch = Regex.Match(text, @"reason:\s*(.+)$", RegexOptions.IgnoreCase);
                var reason = reasonMatch.Success
                    ? reasonMatch.Groups[1].Value.Trim()
                    : null;

                object paramsObj = string.IsNullOrWhiteSpace(reason)
                    ? new { order_id = orderId }
                    : new { order_id = orderId, reason };
                var paramsJson = JsonSerializer.Serialize(paramsObj);
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult { Handled = true, FunctionName = fn.Name, Result = result, ParametersJson = paramsJson };
            }
        }

        // Maker-checker: reject approval (before RejectOrder — "reject approval" contains "reject").
        if (IsRejectApprovalIntent(text) && (text.Contains("đơn") || text.Contains("order") || OrderIdPattern().IsMatch(text)))
        {
            var fn = _registry.GetByName("RejectApproval");
            if (fn is not null)
            {
                var orderMatch = OrderIdPattern().Match(text);
                var orderId = orderMatch.Success ? orderMatch.Groups[1].Value.PadLeft(10, '0') : "0000000000";
                var commentMatch = Regex.Match(text, @"comment:\s*(.+)$", RegexOptions.IgnoreCase);
                var comment = commentMatch.Success ? commentMatch.Groups[1].Value.Trim() : null;

                var paramsJson = JsonSerializer.Serialize(new { order_id = orderId, comment });
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult { Handled = true, FunctionName = fn.Name, Result = result, ParametersJson = paramsJson };
            }
        }

        // Pattern: Reject Order (reason codes) — not cancel/hủy (→ CancelOrder), not force*, not reject approval
        if (text.Contains("reject")
            && (text.Contains("đơn") || text.Contains("order"))
            && !IsForceCancelIntent(text)
            && !IsCancelOrderIntent(text)
            && !IsRejectApprovalIntent(text))
        {
            var fn = _registry.GetByName("RejectOrder");
            if (fn is not null)
            {
                var orderMatch = OrderIdPattern().Match(text);
                var orderId = orderMatch.Success ? orderMatch.Groups[1].Value.PadLeft(10, '0') : "0000000000";

                var reasonMatch = Regex.Match(text, @"reason:\s*(.+)");
                var reasonCode = reasonMatch.Success
                    ? SalesOrderRejectionReasons.ToCanonicalCode(reasonMatch.Groups[1].Value)
                    : InferRejectionReasonCode(text);

                var paramsObj = new { order_id = orderId, reason_code = reasonCode };
                var paramsJson = JsonSerializer.Serialize(paramsObj);
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult { Handled = true, FunctionName = fn.Name, Result = result, ParametersJson = paramsJson };
            }
        }

        // Pattern: Forward Order — show confirm card (recipient optional in NL)
        if ((text.Contains("forward") || text.Contains("chuyển") || text.Contains("bàn giao") || text.Contains("ban giao"))
            && (text.Contains("đơn") || text.Contains("order") || OrderIdPattern().IsMatch(text)))
        {
            var fn = _registry.GetByName("ForwardOrder");
            if (fn is not null)
            {
                var orderMatch = OrderIdPattern().Match(text);
                var orderId = orderMatch.Success ? orderMatch.Groups[1].Value.PadLeft(10, '0') : "0000000000";

                var toMatch = Regex.Match(
                    text,
                    @"(?:to|cho|tới|toi)\s+(DEV-\d+|[\w.\-]+@[\w.\-]+|[^\s,]+(?:\s+[^\s,]+){0,3})",
                    RegexOptions.IgnoreCase);
                var forwardTo = toMatch.Success ? toMatch.Groups[1].Value.Trim().TrimEnd('.', '!', '?') : null;

                object paramsObj = string.IsNullOrWhiteSpace(forwardTo)
                    ? new { order_id = orderId }
                    : new { order_id = orderId, forward_to_user = forwardTo };

                var paramsJson = JsonSerializer.Serialize(paramsObj);
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult { Handled = true, FunctionName = fn.Name, Result = result, ParametersJson = paramsJson };
            }
        }

        // Pattern: Request release (maker) — must run before ReleaseOrder.
        // "request release for order …" must NOT map to Manager ReleaseOrder.
        if (IsRequestReleaseIntent(text) && (text.Contains("đơn") || text.Contains("order") || OrderIdPattern().IsMatch(text)))
        {
            var fn = _registry.GetByName("RequestRelease");
            if (fn is not null)
            {
                var orderMatch = OrderIdPattern().Match(text);
                var orderId = orderMatch.Success ? orderMatch.Groups[1].Value.PadLeft(10, '0') : "0000000000";

                var commentMatch = Regex.Match(text, @"comment:\s*(.+)");
                var commentStr = commentMatch.Success ? commentMatch.Groups[1].Value.Trim() : null;

                var paramsObj = new { order_id = orderId, comment = commentStr };
                var paramsJson = JsonSerializer.Serialize(paramsObj);
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult { Handled = true, FunctionName = fn.Name, Result = result, ParametersJson = paramsJson };
            }
        }

        // Pattern: Approve / release order (checker / direct SAP release) — not force release
        if ((text.Contains("phê duyệt") || text.Contains("phe duyet")
                || text.Contains("duyệt đơn") || text.Contains("duyet don")
                || text.Contains("duyệt order") || text.Contains("duyet order")
                || text.Contains("approve") || text.Contains("release"))
            && (text.Contains("đơn") || text.Contains("don") || text.Contains("order") || OrderIdPattern().IsMatch(text))
            && !IsRequestReleaseIntent(text)
            && !IsForceReleaseIntent(text)
            && !IsRejectApprovalIntent(text))
        {
            var fn = _registry.GetByName("ReleaseOrder");
            if (fn is not null)
            {
                var orderMatch = OrderIdPattern().Match(text);
                var orderId = orderMatch.Success ? orderMatch.Groups[1].Value.PadLeft(10, '0') : "0000000000";

                var commentMatch = Regex.Match(text, @"comment:\s*(.+)");
                var commentStr = commentMatch.Success ? commentMatch.Groups[1].Value.Trim() : "Approved via Teams Bot";

                var paramsObj = new { order_id = orderId, comment = commentStr };
                var paramsJson = JsonSerializer.Serialize(paramsObj);
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult { Handled = true, FunctionName = fn.Name, Result = result, ParametersJson = paramsJson };
            }
        }

        // Pattern: KPI
        if (text.Contains("kpi"))
        {
            var fn = _registry.GetByName("GetKpiSummary");
            if (fn is not null)
            {
                var paramsJson = "{}";
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult { Handled = true, FunctionName = fn.Name, Result = result, ParametersJson = paramsJson };
            }
        }

        // Pattern: Overdue orders (before generic list — "show overdue orders" contains "order")
        if (IsOverdueOrdersIntent(text))
        {
            var fn = _registry.GetByName("GetOverdueOrders");
            if (fn is not null)
            {
                var args = new Dictionary<string, object?>();
                var customerIdOrName = ExtractCustomerIdOrName(text);
                var salesOrg = ExtractSalesOrg(text);
                if (customerIdOrName is not null)
                    args["customerIdOrName"] = customerIdOrName;
                if (salesOrg is not null)
                    args["salesOrg"] = salesOrg;

                var daysMatch = Regex.Match(text, @"(\d+)\s*(?:days?|ngày|ngay)");
                if (daysMatch.Success && int.TryParse(daysMatch.Groups[1].Value, out var days) && days > 0)
                    args["daysPastDue"] = days;

                var paramsJson = JsonSerializer.Serialize(args);
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult
                {
                    Handled = true,
                    FunctionName = fn.Name,
                    Result = result,
                    ParametersJson = paramsJson
                };
            }
        }

        // Pattern 2: List orders — "show orders", "đơn hàng gần đây", "my sales orders"
        if (text.Contains("order") || text.Contains("đơn") || text.Contains("don"))
        {
            var fn = _registry.GetByName("GetSalesOrders");
            if (fn is null)
            {
                return new DispatchResult
                {
                    Handled = false,
                    Reason = "getSalesOrders is not registered"
                };
            }

            var customerIdOrName = ExtractCustomerIdOrName(text);
            var salesOrg = ExtractSalesOrg(text);
            var ownedByMe = IsMyOrdersIntent(text);
            var statusOpen = text.Contains("open")
                || text.Contains("mở")
                || text.Contains("dang mo")
                || text.Contains("đang mở");

            var args = new Dictionary<string, object?>();
            if (customerIdOrName is not null)
                args["customerIdOrName"] = customerIdOrName;
            if (salesOrg is not null)
                args["salesOrg"] = salesOrg;
            if (ownedByMe)
                args["ownedByMe"] = true;
            if (statusOpen)
                args["status"] = "Open";

            var paramsJson = JsonSerializer.Serialize(args);
            using var doc = JsonDocument.Parse(paramsJson);

            var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
            return new DispatchResult
            {
                Handled = true,
                FunctionName = fn.Name,
                Result = result,
                ParametersJson = paramsJson
            };
        }

        return new DispatchResult { Handled = false, Reason = "intent unclear" };
    }

    private static string? ExtractCustomerIdOrName(string text)
    {
        var uscu = Regex.Match(text, @"(uscu_[a-z0-9]+)", RegexOptions.IgnoreCase);
        if (uscu.Success)
            return uscu.Groups[1].Value.ToUpperInvariant();

        var labeled = Regex.Match(
            text,
            @"(?:customer|khách hàng|khach hang)\s+(.+)$",
            RegexOptions.IgnoreCase);
        if (labeled.Success)
        {
            var value = CleanCustomerCapture(labeled.Groups[1].Value);
            if (value is not null)
                return value;
        }

        var ofMatch = Regex.Match(
            text,
            @"(?:orders?|đơn(?:\s+hàng)?|don(?:\s+hang)?)\s+(?:of|của|cua|for)\s+(.+)$",
            RegexOptions.IgnoreCase);
        if (ofMatch.Success)
        {
            var raw = ofMatch.Groups[1].Value.Trim();
            if (raw.StartsWith("customer ", StringComparison.OrdinalIgnoreCase))
                raw = raw["customer ".Length..];
            var value = CleanCustomerCapture(raw);
            if (value is not null)
                return value;
        }

        return null;
    }

    private static string? CleanCustomerCapture(string raw)
    {
        var value = raw.Trim().TrimEnd('.', '!', '?');
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Avoid treating sales-org filters as customer names ("orders of TV01").
        if (IsKnownSalesOrg(value))
            return null;

        // "đơn của tôi" / "orders of me" — ownership intent, not a customer name.
        if (value is "tôi" or "toi" or "me" or "myself")
            return null;

        return value;
    }

    private static string? ExtractSalesOrg(string text)
    {
        foreach (var org in KnownSalesOrgs)
        {
            if (text.Contains(org, StringComparison.OrdinalIgnoreCase))
                return org.ToUpperInvariant();
        }

        return null;
    }

    private static readonly HashSet<string> KnownSalesOrgs = new(StringComparer.OrdinalIgnoreCase)
    {
        "TV01", "FU24", "UE00", "UW00", "DN00", "DS00"
    };

    private static bool IsKnownSalesOrg(string value) => KnownSalesOrgs.Contains(value.Trim());

    private static bool IsMyOrdersIntent(string text) =>
        text.Contains("my sales")
        || text.Contains("my order")
        || text.Contains("của tôi")
        || text.Contains("cua toi")
        || text.Contains("đơn hàng của tôi")
        || text.Contains("don hang cua toi")
        || (text.Contains("my") && (text.Contains("order") || text.Contains("orders")));

    private static bool IsMyProfileIntent(string text) =>
        text is "my profile" or "my info" or "my account"
        || text.Contains("my profile")
        || text.Contains("thông tin của tôi")
        || text.Contains("thong tin cua toi")
        || text.Contains("hồ sơ của tôi")
        || text.Contains("ho so cua toi")
        || text.Contains("thông tin tài khoản")
        || text.Contains("thong tin tai khoan");

    private static bool IsOverdueOrdersIntent(string text) =>
        text.Contains("overdue")
        || text.Contains("quá hạn")
        || text.Contains("qua han")
        || text.Contains("giao trễ")
        || text.Contains("giao tre")
        || text.Contains("trễ hạn")
        || text.Contains("tre han")
        || text.Contains("past due")
        || text.Contains("late delivery")
        || text.Contains("delayed shipment")
        || text.Contains("delayed order");

    private static bool IsRequestReleaseIntent(string text) =>
        text.Contains("request release")
        || text.Contains("yêu cầu duyệt")
        || text.Contains("yeu cau duyet")
        || text.Contains("yêu cầu release")
        || text.Contains("yeu cau release")
        || text.Contains("yêu cầu giải phóng")
        || text.Contains("yeu cau giai phong")
        || text.Contains("xin duyệt")
        || text.Contains("xin duyet")
        || text.Contains("xin release")
        || text.Contains("submit for approval")
        || text.Contains("send for approval")
        || (text.Contains("request") && text.Contains("release"));

    private static bool IsPendingApprovalsIntent(string text) =>
        text.Contains("pending approval")
        || text.Contains("pending approvals")
        || text.Contains("show pending")
        || text.Contains("list pending")
        || text.Contains("chờ duyệt")
        || text.Contains("cho duyet")
        || text.Contains("đang chờ duyệt")
        || text.Contains("dang cho duyet")
        || text.Contains("duyệt pending")
        || text.Contains("duyet pending")
        || text.Contains("danh sách chờ duyệt")
        || text.Contains("danh sach cho duyet")
        || text.Contains("danh sách pending")
        || text.Contains("danh sach pending");

    private static bool IsUpdateReferenceIntent(string text) =>
        text.Contains("update reference")
        || text.Contains("update po reference")
        || text.Contains("change reference")
        || text.Contains("update po ref")
        || (text.Contains("cập nhật") && (text.Contains("reference") || text.Contains("tham chiếu") || text.Contains("po")))
        || (text.Contains("cap nhat") && (text.Contains("reference") || text.Contains("tham chieu") || text.Contains("po")));

    private static bool IsEditOrderIntent(string text) =>
        !IsUpdateReferenceIntent(text)
        && (
            text.Contains("edit order")
            || text.Contains("edit so")
            || text.Contains("edit sales order")
            || (text.Contains("sửa") && text.Contains("đơn"))
            || (text.Contains("sua") && text.Contains("don"))
            || text.Contains("cập nhật đơn")
            || text.Contains("cap nhat don"));

    private static bool IsForceCancelIntent(string text) =>
        text.Contains("force cancel")
        || text.Contains("forcecancel")
        || text.Contains("force-cancel")
        || text.Contains("ép hủy")
        || text.Contains("ep huy");

    private static bool IsCancelOrderIntent(string text) =>
        !IsForceCancelIntent(text)
        && !IsRejectApprovalIntent(text)
        && (
            text.Contains("cancel order")
            || text.Contains("cancel so")
            || text.Contains("cancel sales order")
            || (text.Contains("hủy") && text.Contains("đơn"))
            || (text.Contains("huỷ") && text.Contains("đơn"))
            || (text.Contains("huy") && text.Contains("don")));

    private static bool IsForceReleaseIntent(string text) =>
        text.Contains("force release")
        || text.Contains("forcerelease")
        || text.Contains("force-release")
        || text.Contains("ép release")
        || text.Contains("ep release");

    private static bool IsRejectApprovalIntent(string text) =>
        text.Contains("reject approval")
        || text.Contains("rejectapproval")
        || text.Contains("từ chối duyệt")
        || text.Contains("tu choi duyet")
        || text.Contains("từ chối phê duyệt")
        || text.Contains("tu choi phe duyet")
        || text.Contains("không duyệt")
        || text.Contains("khong duyet")
        || (text.Contains("reject") && text.Contains("approval"));

    private static string InferRejectionReasonCode(string text)
    {
        if (text.Contains("sai giá") || text.Contains("price") || text.Contains("đắt") || text.Contains("expensive"))
            return "PRICE_ISSUE";
        if (text.Contains("hết hàng") || text.Contains("stock") || text.Contains("inventory"))
            return "OUT_OF_STOCK";
        if (text.Contains("khách hủy") || text.Contains("customer cancel") || text.Contains("cancelled by customer"))
            return "CUSTOMER_CANCEL";
        if (text.Contains("sai hàng") || text.Contains("wrong item") || text.Contains("wrong material"))
            return "WRONG_ITEM";
        if (text.Contains("giao hàng") || text.Contains("delivery date") || text.Contains("ngày giao"))
            return "DELIVERY_DATE";
        if (text.Contains("tín dụng") || text.Contains("credit") || text.Contains("payment"))
            return "CREDIT_ISSUE";
        if (text.Contains("trùng") || text.Contains("duplicate"))
            return "DUPLICATE_ORDER";
        return "OTHER";
    }

    [GeneratedRegex(@"(\d{4,10})")]
    private static partial Regex OrderIdPattern();
}


