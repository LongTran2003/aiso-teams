using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Releases (approves) a Sales Order in SAP.
/// Maps to AI function schema <c>ReleaseOrder</c>.
/// Sprint 2-3: returns mock success. Sprint 4: calls SAP RAP action.
/// </summary>
public sealed class ReleaseOrderFunction : IFunction
{
    private readonly ILogger<ReleaseOrderFunction> _logger;

    public ReleaseOrderFunction(ILogger<ReleaseOrderFunction> logger)
    {
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

    public Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
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
            return Task.FromResult(FunctionResult.Fail("Missing required parameter: order_id"));
        }

        _logger.LogInformation(
            "ReleaseOrder: orderId={OrderId}, comment={Comment}", orderId, comment);

        // Sprint 2-3: mock success. Sprint 4: call SAP RAP action.
        var result = new
        {
            order_id = orderId,
            action = "Released",
            comment,
            message = $"Sales order {orderId} has been released successfully."
        };

        return Task.FromResult(FunctionResult.Ok(result));
    }
}

