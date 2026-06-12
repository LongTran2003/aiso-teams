using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Forwards a Sales Order to another user for review/approval.
/// Maps to AI function schema <c>ForwardOrder</c>.
/// Sprint 2-3: returns mock success. Sprint 4: calls SAP substitution service.
/// </summary>
public sealed class ForwardOrderFunction : IFunction
{
    private readonly ILogger<ForwardOrderFunction> _logger;

    public ForwardOrderFunction(ILogger<ForwardOrderFunction> logger)
    {
        _logger = logger;
    }

    public string Name => "ForwardOrder";

    public string Description =>
        "Forward a sales order to another user for further review or approval.";

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
              "description": "Target recipient username, name, or email."
            }
          },
          "required": ["order_id", "forward_to_user"]
        }
        """;

    public Task<FunctionResult> ExecuteAsync(
        JsonElement parameters, CancellationToken ct = default)
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
            return Task.FromResult(FunctionResult.Fail("Missing required parameter: order_id"));
        }

        if (string.IsNullOrWhiteSpace(forwardTo))
        {
            return Task.FromResult(FunctionResult.Fail("Missing required parameter: forward_to_user"));
        }

        _logger.LogInformation(
            "ForwardOrder: orderId={OrderId}, forwardTo={ForwardTo}", orderId, forwardTo);

        // Sprint 2-3: mock success. Sprint 4: call SAP substitution service.
        var result = new
        {
            order_id = orderId,
            action = "Forwarded",
            forward_to_user = forwardTo,
            message = $"Sales order {orderId} has been forwarded to {forwardTo}."
        };

        return Task.FromResult(FunctionResult.Ok(result));
    }
}
