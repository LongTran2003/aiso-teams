using System.Text.Json;
using AISO.Domain.Approvals;
using AISO.Domain.SalesOrders;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Maker step: Employee submits a sales order for Manager approval (does not call SAP release).
/// </summary>
public sealed class RequestReleaseFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly IOrderApprovalService _approvals;
    private readonly ILogger<RequestReleaseFunction> _logger;

    public RequestReleaseFunction(
        ISapClient sap,
        IOrderApprovalService approvals,
        ILogger<RequestReleaseFunction> logger)
    {
        _sap = sap;
        _approvals = approvals;
        _logger = logger;
    }

    public string Name => "RequestRelease";

    public string Description =>
        "Submit a sales order for release approval (maker-checker). " +
        "Does not release the order; a Manager must ApproveOrder afterwards.";

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
              "description": "Optional note for the approving manager."
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

        try
        {
            var order = await _sap.GetSalesOrderByIdAsync(orderId, ct);
            if (order is null)
            {
                return FunctionResult.Fail($"Sales order {orderId} was not found in SAP.");
            }

            if (SalesOrderWorkflow.BlocksReleaseRejectForward(order.Status))
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildBlockedMessage(order.Status, "Request release"));
            }

            if (order.HasInvalidMaterial)
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildInvalidMaterialBlockedMessage("Request release"),
                    "VALIDATION");
            }

            var pending = await _approvals.GetPendingBySoNumberAsync(order.SoNumber, ct);
            if (pending is not null)
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildPendingApprovalBlockedMessage(
                        "Request release",
                        pending.RequestedBySapUser));
            }

            var request = await _approvals.RequestReleaseAsync(
                order.SoNumber,
                requestingSapUser,
                order.SalesOrg,
                comment,
                ct);

            _logger.LogInformation(
                "RequestRelease: so={SoNumber} by={User} salesOrg={SalesOrg}",
                request.SoNumber, requestingSapUser, request.SalesOrg);

            return FunctionResult.Ok(new
            {
                order_id = request.SoNumber,
                action = "ReleaseRequested",
                sales_org = request.SalesOrg,
                comment,
                message = $"Sales order {request.SoNumber} was submitted for release approval. Waiting for a Manager to approve — the order is not released yet."
            });
        }
        catch (InvalidOperationException ex)
        {
            return FunctionResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request release for {OrderId}", orderId);
            return FunctionResult.Fail($"Failed to request release: {ex.Message}");
        }
    }
}
