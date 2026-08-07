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

        // Pattern: Create Order (VI: "tạo đơn" / EN: "create order" | "create so" | "create sales order")
        if ((text.Contains("tạo") && text.Contains("đơn"))
            || text.Contains("create order")
            || text.Contains("create so")
            || text.Contains("create sales order"))
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

                var customerId = "10100001";
                if (custMatch.Success)
                {
                    customerId = (custMatch.Groups[1].Success ? custMatch.Groups[1].Value
                        : custMatch.Groups[2].Success ? custMatch.Groups[2].Value
                        : custMatch.Groups[3].Value).ToUpperInvariant();
                }

                var material = matMatch.Success ? matMatch.Groups[1].Value.ToUpperInvariant() : "TG11";
                var qty = 1;
                if (qtyMatch.Success)
                {
                    qty = int.Parse(qtyMatch.Groups[1].Success ? qtyMatch.Groups[1].Value : qtyMatch.Groups[2].Value);
                }

                var paramsObj = new
                {
                    customer = customerId,
                    items = new[] { new { material = material, qty = qty } }
                };

                var paramsJson = JsonSerializer.Serialize(paramsObj);
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult { Handled = true, FunctionName = fn.Name, Result = result, ParametersJson = paramsJson };
            }
        }

        // Pattern: Update Reference
        if (text.Contains("cập nhật") && text.Contains("reference"))
        {
            var fn = _registry.GetByName("UpdateOrderReference");
            if (fn is not null)
            {
                var orderMatch = OrderIdPattern().Match(text);
                var refMatch = Regex.Match(text, @"thành '([^']+)'", RegexOptions.IgnoreCase);

                var orderId = orderMatch.Success ? orderMatch.Groups[1].Value.PadLeft(10, '0') : "0000000000";
                var newRef = refMatch.Success ? refMatch.Groups[1].Value : "Updated Reference";

                var paramsObj = new { order_id = orderId, new_reference = newRef };
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

        // Pattern: Cancel/Reject Order (not force*, not reject approval)
        if ((text.Contains("hủy") || text.Contains("huỷ") || text.Contains("reject") || text.Contains("cancel"))
            && (text.Contains("đơn") || text.Contains("order"))
            && !IsForceCancelIntent(text)
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
        if ((text.Contains("phê duyệt") || text.Contains("approve") || text.Contains("release"))
            && (text.Contains("đơn") || text.Contains("order") || OrderIdPattern().IsMatch(text))
            && !IsRequestReleaseIntent(text)
            && !IsForceReleaseIntent(text))
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

        // Pattern 2: List orders — "show orders", "đơn hàng gần đây", "my sales orders"
        if (text.Contains("order") || text.Contains("đơn"))
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

            var paramsObj = customerIdOrName != null
                ? new { customerIdOrName }
                : (object)new { };

            var paramsJson = JsonSerializer.Serialize(paramsObj);
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

        return value;
    }

    private static readonly HashSet<string> KnownSalesOrgs = new(StringComparer.OrdinalIgnoreCase)
    {
        "TV01", "FU24", "UE00", "UW00", "DN00", "DS00"
    };

    private static bool IsKnownSalesOrg(string value) => KnownSalesOrgs.Contains(value.Trim());

    private static bool IsRequestReleaseIntent(string text) =>
        text.Contains("request release")
        || text.Contains("yêu cầu duyệt")
        || text.Contains("yêu cầu release")
        || text.Contains("yêu cầu giải phóng")
        || text.Contains("xin duyệt")
        || text.Contains("submit for approval")
        || text.Contains("send for approval")
        || (text.Contains("request") && text.Contains("release"));

    private static bool IsForceCancelIntent(string text) =>
        text.Contains("force cancel")
        || text.Contains("forcecancel")
        || text.Contains("force-cancel")
        || text.Contains("ép hủy")
        || text.Contains("ep huy");

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


