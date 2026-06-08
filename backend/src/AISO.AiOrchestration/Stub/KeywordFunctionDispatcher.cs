using System.Text.Json;

namespace AISO.AiOrchestration.Stub;

/// <summary>
/// Placeholder dispatcher using simple keyword matching.
/// Replaced by Azure OpenAI function-calling dispatcher in Sprint 3 by the AI team.
/// </summary>
public sealed class KeywordFunctionDispatcher : IFunctionDispatcher
{
    private readonly IFunctionRegistry _registry;

    public KeywordFunctionDispatcher(IFunctionRegistry registry)
    {
        _registry = registry;
    }

    public async Task<DispatchResult> DispatchAsync(string userMessage, CancellationToken ct = default)
    {
        var text = userMessage.Trim().ToLowerInvariant();

        if (text.Contains("order") || text.Contains("đơn"))
        {
            var fn = _registry.GetByName("getSalesOrders");
            if (fn is null)
            {
                return new DispatchResult
                {
                    Handled = false,
                    Reason = "getSalesOrders is not registered"
                };
            }

            using var emptyParams = JsonDocument.Parse("{}");
            var result = await fn.ExecuteAsync(emptyParams.RootElement, ct);
            return new DispatchResult
            {
                Handled = true,
                FunctionName = fn.Name,
                Result = result
            };
        }

        return new DispatchResult { Handled = false, Reason = "intent unclear" };
    }
}
