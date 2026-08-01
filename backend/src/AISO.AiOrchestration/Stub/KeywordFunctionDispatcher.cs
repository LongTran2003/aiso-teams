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

        // Pattern: Create Order
        if (text.Contains("tạo") && text.Contains("đơn"))
        {
            var fn = _registry.GetByName("CreateOrder");
            if (fn is not null)
            {
                var custMatch = Regex.Match(text, @"(uscu_[a-z0-9]+)", RegexOptions.IgnoreCase);
                var matMatch = Regex.Match(text, @"mặt hàng ([a-z0-9]+)", RegexOptions.IgnoreCase);
                var qtyMatch = Regex.Match(text, @"số lượng (\d+)");

                var customerId = custMatch.Success ? custMatch.Groups[1].Value.ToUpperInvariant() : "10100001";
                var material = matMatch.Success ? matMatch.Groups[1].Value.ToUpperInvariant() : "TG11";
                var qty = qtyMatch.Success ? int.Parse(qtyMatch.Groups[1].Value) : 1;

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

        // Pattern: Cancel/Reject Order
        if ((text.Contains("hủy") || text.Contains("huỷ") || text.Contains("reject") || text.Contains("cancel")) && (text.Contains("đơn") || text.Contains("order")))
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

        // Pattern: Forward Order
        if ((text.Contains("forward") || text.Contains("chuyển")) && (text.Contains("đơn") || text.Contains("order")))
        {
            var fn = _registry.GetByName("ForwardOrder");
            if (fn is not null)
            {
                var orderMatch = OrderIdPattern().Match(text);
                var orderId = orderMatch.Success ? orderMatch.Groups[1].Value.PadLeft(10, '0') : "0000000000";

                var toMatch = Regex.Match(text, @"(?:to|cho)\s+([^\s]+)");
                var forwardTo = toMatch.Success ? toMatch.Groups[1].Value : "manager@aiso.com";

                var paramsObj = new { order_id = orderId, forward_to_user = forwardTo };
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

        // Pattern: Approve / release order (checker / direct SAP release)
        if ((text.Contains("phê duyệt") || text.Contains("approve") || text.Contains("release"))
            && (text.Contains("đơn") || text.Contains("order"))
            && !IsRequestReleaseIntent(text))
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


