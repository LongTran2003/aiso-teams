using System.Text.Json;
using AISO.Domain.SalesOrders;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Releases (approves) a Sales Order in SAP.
/// Maps to AI function schema <c>ReleaseOrder</c>.
/// Sprint 2-3: returns mock success. Sprint 4: calls SAP RAP action.
/// </summary>
public sealed class ReleaseOrderFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly ILogger<ReleaseOrderFunction> _logger;

    public ReleaseOrderFunction(ISapClient sap, ILogger<ReleaseOrderFunction> logger)
    {
        _sap = sap;
        _logger = logger;
    }

    public string Name => "ReleaseOrder";

    public string Description =>
        "Approve and release a pending sales order in the SAP ERP system.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "order_id": {
              "type": "string",
              "description": "The unique sales order identifier (e.g. '0000005001')."
            },
            "comment": {
              "type": "string",
              "description": "Optional note explaining the reason for approval."
            }
          },
          "required": ["order_id"]
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var orderId = parameters.TryGetProperty("order_id", out var p)
                      && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

        var comment = parameters.TryGetProperty("comment", out var c)
                      && c.ValueKind == JsonValueKind.String
            ? c.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(orderId))
        {
            return FunctionResult.Fail("Missing required parameter: order_id");
        }

        // Role gating is enforced by RolePolicy in the dispatcher (Manager+).
        // Prefer ApproveOrder for the maker-checker path when a pending request exists.
        _logger.LogInformation(
            "ReleaseOrder: orderId={OrderId}, comment={Comment}, sapUser={SapUser}", orderId, comment, requestingSapUser);

        try
        {
            var existing = await _sap.GetSalesOrderByIdAsync(orderId, ct);
            if (existing is null)
            {
                return FunctionResult.Fail($"Sales order {orderId} was not found in SAP.");
            }

            if (SalesOrderWorkflow.BlocksReleaseRejectForward(existing.Status))
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildBlockedMessage(existing.Status, "Release"));
            }

            // Call SAP RAP action
            var updatedOrder = await _sap.ReleaseOrderAsync(orderId, requestingSapUser, ct);

            // Audit Log
            _logger.LogInformation("AUDIT: User {User} successfully released order {OrderId} with comment: {Comment}", requestingSapUser, orderId, comment ?? "None");

            var result = new
            {
                order_id = updatedOrder.SoNumber,
                action = "Released",
                comment,
                message = $"Sales order {updatedOrder.SoNumber} has been released successfully. Status is now {updatedOrder.Status}."
            };

            return FunctionResult.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release order {OrderId}", orderId);
            return FunctionResult.Fail($"Failed to release order in SAP: {ex.Message}");
        }
    }
}

