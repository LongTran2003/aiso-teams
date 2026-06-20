using System.Text.Json;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Creates a new Sales Order in SAP.
/// Maps to AI function schema <c>CreateOrder</c>.
/// </summary>
public sealed class CreateOrderFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly ILogger<CreateOrderFunction> _logger;

    public CreateOrderFunction(ISapClient sap, ILogger<CreateOrderFunction> logger)
    {
        _sap = sap;
        _logger = logger;
    }

    public string Name => "CreateOrder";

    public string Description =>
        "Create a new sales order in the SAP ERP system.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "customer": {
              "type": "string",
              "description": "The customer ID."
            },
            "sales_org": {
              "type": "string",
              "description": "Sales Organization."
            },
            "currency": {
              "type": "string",
              "description": "Currency."
            },
            "items": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "material": { "type": "string" },
                  "qty": { "type": "number" },
                  "plant": { "type": "string" }
                }
              }
            }
          },
          "required": ["customer"]
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var customer = parameters.TryGetProperty("customer", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : "10100001";
        var salesOrg = parameters.TryGetProperty("sales_org", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : "1010";
        var currency = parameters.TryGetProperty("currency", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : "USD";

        _logger.LogInformation("CreateOrder: customer={Customer}, sapUser={SapUser}", customer, requestingSapUser);

        try
        {
            var dto = new CreateSalesOrderDto
            {
                DocType = "TA",
                SalesOrg = salesOrg ?? "1010",
                DistChannel = "10",
                Division = "00",
                Customer = customer ?? "10100001",
                Currency = currency ?? "USD",
                Items = new List<CreateSalesOrderItemDto>
                {
                    new CreateSalesOrderItemDto { Material = "TG11", Plant = "1010", OrderQty = 1, Unit = "PC" }
                }
            };

            var newOrder = await _sap.CreateSalesOrderAsync(dto, ct);
            var result = new
            {
                order_id = newOrder.SoNumber,
                action = "Created",
                message = $"Sales order {newOrder.SoNumber} has been created."
            };

            return FunctionResult.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create order");
            return FunctionResult.Fail($"Failed to create order in SAP: {ex.Message}");
        }
    }
}

