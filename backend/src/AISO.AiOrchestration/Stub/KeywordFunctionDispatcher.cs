using System.Text.Json;
using System.Text.RegularExpressions;

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

    public async Task<DispatchResult> DispatchAsync(string userMessage, string requestingSapUser, CancellationToken ct = default)
    {
        var text = userMessage.Trim().ToLowerInvariant();

        // Pattern 1: Check specific order — "kiểm tra đơn hàng 5001" or "check order 5001"
        if (text.Contains("kiểm tra") || text.Contains("check"))
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

        // Pattern: Cancel Order
        if ((text.Contains("hủy") || text.Contains("huỷ")) && text.Contains("đơn"))
        {
            var fn = _registry.GetByName("RejectOrder");
            if (fn is not null)
            {
                var orderMatch = OrderIdPattern().Match(text);
                var orderId = orderMatch.Success ? orderMatch.Groups[1].Value.PadLeft(10, '0') : "0000000000";

                var paramsObj = new { order_id = orderId, reason_code = "OTHER" };
                var paramsJson = JsonSerializer.Serialize(paramsObj);
                using var doc = JsonDocument.Parse(paramsJson);
                var result = await fn.ExecuteAsync(doc.RootElement, requestingSapUser, ct);
                return new DispatchResult { Handled = true, FunctionName = fn.Name, Result = result, ParametersJson = paramsJson };
            }
        }

        // Pattern 2: List orders — "show orders", "đơn hàng gần đây"
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

            var customerIdMatch = Regex.Match(text, @"(uscu_[a-z0-9]+)", RegexOptions.IgnoreCase);
            var customerId = customerIdMatch.Success ? customerIdMatch.Groups[1].Value.ToUpperInvariant() : null;

            var paramsObj = customerId != null
                ? new { customerIdOrName = customerId }
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

    [GeneratedRegex(@"(\d{4,10})")]
    private static partial Regex OrderIdPattern();
}


