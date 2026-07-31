using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Alias for <see cref="CheckOrderStatusFunction"/> matching AI schema
/// <c>ai/functions/get_order_detail.json</c> (<c>GetOrderDetail</c>).
/// </summary>
public sealed class GetOrderDetailFunction : IFunction
{
    private readonly CheckOrderStatusFunction _inner;
    private readonly ILogger<GetOrderDetailFunction> _logger;

    public GetOrderDetailFunction(
        CheckOrderStatusFunction inner,
        ILogger<GetOrderDetailFunction> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public string Name => "GetOrderDetail";

    public string Description =>
        "Get full details of a specific SAP Sales Order. Requires explicit order_id.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "order_id": {
              "type": "string",
              "description": "SAP Sales Order number."
            }
          },
          "required": ["order_id"]
        }
        """;

    public Task<FunctionResult> ExecuteAsync(
        JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        _logger.LogInformation("Executing GetOrderDetail (delegates to CheckOrderStatus)");
        return _inner.ExecuteAsync(parameters, requestingSapUser, ct);
    }
}
