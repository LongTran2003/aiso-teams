using System.Text.Json;
using AISO.Domain.Approvals;
using AISO.Domain.SalesOrders;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// NL / AI forward: validate the sales order and return a confirm-card payload.
/// Does <b>not</b> call SAP — that happens on Adaptive Card <c>forward_so_confirm</c>
/// after the user picks a recipient and confirms.
/// </summary>
public sealed class ForwardOrderFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly IOrderApprovalService _approvals;
    private readonly ILogger<ForwardOrderFunction> _logger;

    public ForwardOrderFunction(
        ISapClient sap,
        IOrderApprovalService approvals,
        ILogger<ForwardOrderFunction> logger)
    {
        _sap = sap;
        _approvals = approvals;
        _logger = logger;
    }

    public string Name => "ForwardOrder";

    public string Description =>
        "Prepare forwarding a sales order you own to another SAP user. " +
        "Validates the order and returns a confirmation step with recipient picker — " +
        "does not transfer ownership until the user confirms. " +
        "Call with order_id even when the recipient is unknown; pass forward_to_user when stated.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "order_id": {
              "type": "string",
              "description": "The unique sales order identifier (e.g. '0000005001')."
            },
            "forward_to_user": {
              "type": "string",
              "description": "Optional suggested recipient (SAP id, name, or email). Used to pre-select on the confirm card when it matches a linked user."
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

        var forwardTo = parameters.TryGetProperty("forward_to_user", out var f)
                        && f.ValueKind == JsonValueKind.String
            ? f.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(orderId))
        {
            return FunctionResult.Fail("Missing required parameter: order_id");
        }

        _logger.LogInformation(
            "ForwardOrder confirm step: orderId={OrderId}, suggestedRecipient={ForwardTo}, sapUser={SapUser}",
            orderId, forwardTo, requestingSapUser);

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
                    SalesOrderWorkflow.BuildBlockedMessage(existing.Status, "Forward"),
                    "VALIDATION");
            }

            if (existing.HasInvalidMaterial)
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildInvalidMaterialBlockedMessage("Forward"),
                    "VALIDATION");
            }

            var pending = await _approvals.GetPendingBySoNumberAsync(existing.SoNumber, ct);
            if (pending is not null)
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildPendingApprovalBlockedMessage(
                        "Forward",
                        pending.RequestedBySapUser),
                    "VALIDATION");
            }

            if (!SalesOrderWorkflow.IsCurrentOwner(existing.OwnerSapUser, requestingSapUser)
                && !string.IsNullOrWhiteSpace(existing.OwnerSapUser))
            {
                return FunctionResult.Fail(
                    SalesOrderWorkflow.BuildNotOwnerBlockedMessage("Forward", existing.OwnerSapUser),
                    "VALIDATION");
            }

            return FunctionResult.Ok(new ConfirmForwardResponse(
                existing.SoNumber,
                string.IsNullOrWhiteSpace(forwardTo) ? null : forwardTo.Trim(),
                string.IsNullOrWhiteSpace(existing.SalesOrg) ? null : existing.SalesOrg.Trim()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare forward for order {OrderId}", orderId);
            return FunctionResult.Fail($"Failed to prepare forward: {ex.Message}");
        }
    }
}

/// <summary>
/// Payload telling the bot to show <c>confirm-forward</c> before calling SAP.
/// </summary>
public sealed record ConfirmForwardResponse(
    string SoNumber,
    string? SuggestedRecipient = null,
    string? SalesOrg = null);
