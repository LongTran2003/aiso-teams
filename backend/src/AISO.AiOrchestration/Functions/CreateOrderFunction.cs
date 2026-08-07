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
            "customer": { "type": "string", "description": "The customer ID (e.g. '10100001')." },
            "doc_type": { "type": "string", "description": "The document type (default 'TA')." },
            "sales_org": { "type": "string", "description": "Sales Organization (e.g. '1010')." },
            "dist_channel": { "type": "string", "description": "Distribution Channel (e.g. '10')." },
            "division": { "type": "string", "description": "Division (e.g. '00')." },
            "currency": { "type": "string", "description": "Currency (default 'USD')." },
            "items": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "material": { "type": "string", "description": "Material code (e.g. 'TG11')." },
                  "qty": { "type": "number", "description": "Order quantity." },
                  "plant": { "type": "string", "description": "Plant (e.g. '1010')." },
                  "unit": { "type": "string", "description": "Unit of measure (e.g. 'PC')." }
                },
                "required": ["material", "qty"]
              }
            }
          },
          "required": ["customer", "items"]
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var customer = parameters.TryGetProperty("customer", out var p1) && p1.ValueKind == JsonValueKind.String ? p1.GetString() : "10100001";
        var docType = parameters.TryGetProperty("doc_type", out var p2) && p2.ValueKind == JsonValueKind.String ? p2.GetString() : "TA";
        var salesOrg = parameters.TryGetProperty("sales_org", out var p3) && p3.ValueKind == JsonValueKind.String ? p3.GetString() : "1010";
        var distChannel = parameters.TryGetProperty("dist_channel", out var p4) && p4.ValueKind == JsonValueKind.String ? p4.GetString() : "10";
        var division = parameters.TryGetProperty("division", out var p5) && p5.ValueKind == JsonValueKind.String ? p5.GetString() : "00";
        var currency = parameters.TryGetProperty("currency", out var p6) && p6.ValueKind == JsonValueKind.String ? p6.GetString() : "USD";

        var itemsList = new List<CreateSalesOrderItemDto>();
        if (parameters.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemsElement.EnumerateArray())
            {
                var material = item.TryGetProperty("material", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : "UNKNOWN";
                var qty = item.TryGetProperty("qty", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetDecimal() : 1m;
                var plant = item.TryGetProperty("plant", out var pl) && pl.ValueKind == JsonValueKind.String ? pl.GetString() : "1010";
                var unit = item.TryGetProperty("unit", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : "PC";

                itemsList.Add(new CreateSalesOrderItemDto
                {
                    Material = material!,
                    OrderQty = qty,
                    Plant = plant!,
                    Unit = unit!
                });
            }
        }
        else
        {
            // Fallback default item if none provided
            itemsList.Add(new CreateSalesOrderItemDto { Material = "TG11", Plant = "1010", OrderQty = 1, Unit = "PC" });
        }

        _logger.LogInformation("CreateOrder: customer={Customer}, itemsCount={Count}, sapUser={SapUser}", customer, itemsList.Count, requestingSapUser);

        try
        {
            var dto = new CreateSalesOrderDto
            {
                DocType = docType!,
                SalesOrg = salesOrg!,
                DistChannel = distChannel!,
                Division = division!,
                Customer = customer!,
                Currency = currency!,
                Items = itemsList,
                RequestingSapUser = requestingSapUser
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

