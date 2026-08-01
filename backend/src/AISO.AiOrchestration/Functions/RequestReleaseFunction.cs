using System.Text.Json;
using AISO.Domain.Approvals;
using AISO.Domain.SalesOrders;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Maker step (NL / AI): validate the sales order and return a confirm-card payload.
/// Does <b>not</b> create the approval row — that happens on Adaptive Card
/// <c>request_release_confirm</c> after the user confirms.
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
        "Prepare a release-approval request for a sales order (maker-checker). " +
        "Validates the order and returns a confirmation step — does not submit until the user confirms. " +
        "A Manager must ApproveOrder after the employee confirms.";

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

            if (!SalesOrderWorkflow.IsCurrentOwner(order.OwnerSapUser, requestingSapUser)
                && !string.IsNullOrWhiteSpace(order.OwnerSapUser))
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildNotOwnerBlockedMessage("Request release", order.OwnerSapUser),
                    "VALIDATION");
            }

            if (SalesOrderWorkflow.BlocksReleaseRejectForward(order.Status))
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildBlockedMessage(order.Status, "Request release"),
                    "VALIDATION");
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
                        pending.RequestedBySapUser),
                    "VALIDATION");
            }

            _logger.LogInformation(
                "RequestRelease confirm step: so={SoNumber} by={User} (not submitted yet)",
                order.SoNumber, requestingSapUser);

            return FunctionResult.Ok(new ConfirmRequestReleaseResponse(
                order.SoNumber,
                string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare request release for {OrderId}", orderId);
            return FunctionResult.Fail($"Failed to prepare request release: {ex.Message}");
        }
    }
}

/// <summary>
/// Payload telling the bot to show <c>confirm-request-release</c> before writing to Postgres.
/// </summary>
public sealed record ConfirmRequestReleaseResponse(string SoNumber, string? Comment = null);
