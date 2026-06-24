using System.Text.Json;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Updates the reference number (e.g. Customer PO) of a Sales Order in SAP.
/// Maps to AI function schema <c>UpdateOrderReference</c>.
/// </summary>
public sealed class UpdateOrderReferenceFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly ILogger<UpdateOrderReferenceFunction> _logger;

    public UpdateOrderReferenceFunction(ISapClient sap, ILogger<UpdateOrderReferenceFunction> logger)
    {
        _sap = sap;
        _logger = logger;
    }

    public string Name => "UpdateOrderReference";

    public string Description =>
        "Update the reference number (like a Customer PO) on an existing sales order in SAP.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "order_id": {
              "type": "string",
              "description": "The unique sales order identifier (e.g. '0000005001')."
            },
            "new_reference": {
              "type": "string",
              "description": "The new reference string or PO number."
            }
          },
          "required": ["order_id", "new_reference"]
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var orderId = parameters.TryGetProperty("order_id", out var p)
                      && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

        var newRef = parameters.TryGetProperty("new_reference", out var r)
                         && r.ValueKind == JsonValueKind.String
            ? r.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(orderId))
        {
            return FunctionResult.Fail("Missing required parameter: order_id");
        }

        if (string.IsNullOrWhiteSpace(newRef))
        {
            return FunctionResult.Fail("Missing required parameter: new_reference");
        }

        _logger.LogInformation(
            "UpdateOrderReference: orderId={OrderId}, newRef={NewRef}, sapUser={SapUser}", orderId, newRef, requestingSapUser);

        try
        {
            var updatedOrder = await _sap.UpdateReferenceAsync(orderId, newRef, requestingSapUser, ct);
            var result = new
            {
                order_id = updatedOrder.SoNumber,
                action = "ReferenceUpdated",
                new_reference = newRef,
                message = $"Sales order {updatedOrder.SoNumber} reference has been updated to {newRef}."
            };

            return FunctionResult.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update reference for order {OrderId}", orderId);
            return FunctionResult.Fail($"Failed to update order reference in SAP: {ex.Message}");
        }
    }
}

