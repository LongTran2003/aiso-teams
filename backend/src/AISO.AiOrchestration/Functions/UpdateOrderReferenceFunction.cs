using System.Text.Json;
using AISO.Domain.SalesOrders;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Prepare update-reference form (confirm card). SAP call runs on Adaptive Card
/// <c>update_ref_confirm</c>.
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
        "Update the reference number (like a Customer PO) on an existing sales order in SAP. " +
        "Returns a confirmation form — does not update until the user confirms.";

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
            return FunctionResult.Fail("Missing required parameter: order_id", "VALIDATION");

        try
        {
            var existing = await _sap.GetSalesOrderByIdAsync(orderId, ct);
            if (existing is null)
                return FunctionResult.Fail($"Sales order {orderId} was not found in SAP.", "NOT_FOUND");

            if (SalesOrderWorkflow.BlocksReject(existing.Status))
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildBlockedMessage(existing.Status, "Update reference"),
                    "VALIDATION");
            }

            if (!SalesOrderWorkflow.IsCurrentOwner(existing.OwnerSapUser, requestingSapUser)
                && !string.IsNullOrWhiteSpace(existing.OwnerSapUser))
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildNotOwnerBlockedMessage("Update reference", existing.OwnerSapUser),
                    "VALIDATION");
            }

            var draft = string.IsNullOrWhiteSpace(newRef) || newRef == "Updated Reference"
                ? (existing.CustomerReference ?? string.Empty)
                : newRef.Trim();

            _logger.LogInformation(
                "UpdateOrderReference confirm step: so={SoNumber} by={User}",
                existing.SoNumber, requestingSapUser);

            return FunctionResult.Ok(new ConfirmUpdateReferenceResponse(
                existing.SoNumber,
                existing.CustomerReference ?? string.Empty,
                draft));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateOrderReference prepare failed for {OrderId}", orderId);
            return FunctionResult.Fail($"Failed to prepare update reference: {ex.Message}", "ACTION_FAILED");
        }
    }
}

/// <summary>Payload telling the bot to show <c>confirm-update-reference.json</c>.</summary>
public sealed record ConfirmUpdateReferenceResponse(
    string SoNumber,
    string CurrentReference,
    string NewReference);
