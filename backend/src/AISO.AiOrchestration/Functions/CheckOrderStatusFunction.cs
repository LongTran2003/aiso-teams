using System.Text.Json;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Checks the status of a specific Sales Order by its SAP SO number.
/// Maps to the AI function schema <c>CheckOrderStatus</c> defined in
/// <c>ai/functions/check_order_status.json</c>.
///
/// The AI sends <c>{ "order_id": "0000005001" }</c>; this function
/// looks up the SO via <see cref="ISapClient.GetSalesOrderByIdAsync"/>.
/// </summary>
public sealed class CheckOrderStatusFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly ILogger<CheckOrderStatusFunction> _logger;

    public CheckOrderStatusFunction(ISapClient sap, ILogger<CheckOrderStatusFunction> logger)
    {
        _sap = sap;
        _logger = logger;
    }

    public string Name => "CheckOrderStatus";

    public string Description =>
        "Check the status and details of a specific Sales Order by its order number.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "order_id": {
              "type": "string",
              "description": "The unique Sales Order number (e.g. '0000005001')."
            }
          },
          "required": ["order_id"]
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(
        JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var orderId = parameters.TryGetProperty("order_id", out var p)
                      && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(orderId))
        {
            return FunctionResult.Fail("Missing required parameter: order_id");
        }

        _logger.LogInformation(
            "Executing CheckOrderStatus: orderId={OrderId}", orderId);

        var order = await _sap.GetSalesOrderByIdAsync(orderId, ct);

        if (order is null)
        {
            _logger.LogWarning("Sales order {OrderId} not found", orderId);
            return FunctionResult.Fail($"Sales order '{orderId}' not found.");
        }

        _logger.LogInformation(
            "CheckOrderStatus found SO {SoNumber}, status={Status}",
            order.SoNumber, order.Status);

        // Return as a single-item list so the existing card builder can handle it
        return FunctionResult.Ok(new[] { order } as IReadOnlyList<Domain.SalesOrders.SalesOrder>);
    }
}
