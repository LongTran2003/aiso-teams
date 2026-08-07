using System.Text.Json;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Prepare create-sales-order form (confirm card). SAP call runs on Adaptive Card
/// <c>create_so_confirm</c>.
/// </summary>
public sealed class CreateOrderFunction : IFunction
{
    private readonly ILogger<CreateOrderFunction> _logger;

    public CreateOrderFunction(ISapClient sap, ILogger<CreateOrderFunction> logger)
    {
        _ = sap;
        _logger = logger;
    }

    public string Name => "CreateOrder";

    public string Description =>
        "Create a new sales order in the SAP ERP system. " +
        "Returns a confirmation form — does not create until the user confirms.";

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

    public Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var customer = ReadString(parameters, "customer") ?? "10100001";
        var salesOrg = ReadString(parameters, "sales_org") ?? "1010";
        var currency = ReadString(parameters, "currency") ?? "USD";
        var material = "TG11";
        var qty = 1m;
        var plant = "1010";
        var unit = "PC";

        if (parameters.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemsElement.EnumerateArray())
            {
                material = ReadString(item, "material") ?? material;
                if (item.TryGetProperty("qty", out var q) && q.ValueKind == JsonValueKind.Number)
                    qty = q.GetDecimal();
                plant = ReadString(item, "plant") ?? plant;
                unit = ReadString(item, "unit") ?? unit;
                break;
            }
        }

        _logger.LogInformation(
            "CreateOrder confirm step: customer={Customer} material={Material} qty={Qty} by={User}",
            customer, material, qty, requestingSapUser);

        return Task.FromResult(FunctionResult.Ok(new ConfirmCreateOrderResponse(
            Customer: customer.Trim(),
            Material: material.Trim().ToUpperInvariant(),
            Qty: qty,
            SalesOrg: salesOrg.Trim(),
            Currency: currency.Trim().ToUpperInvariant(),
            Plant: plant.Trim(),
            Unit: unit.Trim().ToUpperInvariant())));
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
}

/// <summary>Payload telling the bot to show <c>confirm-create.json</c>.</summary>
public sealed record ConfirmCreateOrderResponse(
    string Customer,
    string Material,
    decimal Qty,
    string SalesOrg,
    string Currency,
    string Plant,
    string Unit);
