using System.Text.Json;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Prepare create-sales-order form (confirm card). SAP call runs on Adaptive Card
/// <c>create_so_confirm</c>. Supports up to 5 line items.
/// </summary>
public sealed class CreateOrderFunction : IFunction
{
    public const int MaxLineSlots = 5;

    private readonly ILogger<CreateOrderFunction> _logger;

    public CreateOrderFunction(ISapClient sap, ILogger<CreateOrderFunction> logger)
    {
        _ = sap;
        _logger = logger;
    }

    public string Name => "CreateOrder";

    public string Description =>
        "Create a new sales order in the SAP ERP system with one or more materials. " +
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
        var plant = "1010";
        var unit = "PC";

        var lines = new List<ConfirmCreateOrderLine>();
        if (parameters.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemsElement.EnumerateArray())
            {
                if (lines.Count >= MaxLineSlots)
                    break;

                var material = ReadString(item, "material");
                if (string.IsNullOrWhiteSpace(material))
                    continue;

                var qty = 1m;
                if (item.TryGetProperty("qty", out var q) && q.ValueKind == JsonValueKind.Number)
                    qty = q.GetDecimal();
                if (qty < 1)
                    qty = 1m;

                plant = ReadString(item, "plant") ?? plant;
                unit = ReadString(item, "unit") ?? unit;

                lines.Add(new ConfirmCreateOrderLine(
                    material.Trim().ToUpperInvariant(),
                    qty));
            }
        }

        if (lines.Count == 0)
            lines.Add(new ConfirmCreateOrderLine("TG11", 1m));

        _logger.LogInformation(
            "CreateOrder confirm step: customer={Customer} lines={LineCount} by={User}",
            customer, lines.Count, requestingSapUser);

        return Task.FromResult(FunctionResult.Ok(new ConfirmCreateOrderResponse(
            Customer: customer.Trim(),
            SalesOrg: salesOrg.Trim(),
            Currency: currency.Trim().ToUpperInvariant(),
            Plant: plant.Trim(),
            Unit: unit.Trim().ToUpperInvariant(),
            Lines: lines)));
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
}

public sealed record ConfirmCreateOrderLine(string Material, decimal Qty);

/// <summary>Payload telling the bot to show <c>confirm-create.json</c>.</summary>
public sealed record ConfirmCreateOrderResponse(
    string Customer,
    string SalesOrg,
    string Currency,
    string Plant,
    string Unit,
    IReadOnlyList<ConfirmCreateOrderLine> Lines)
{
    /// <summary>First line material (tests / legacy callers).</summary>
    public string Material => Lines.Count > 0 ? Lines[0].Material : string.Empty;

    /// <summary>First line qty (tests / legacy callers).</summary>
    public decimal Qty => Lines.Count > 0 ? Lines[0].Qty : 1m;
}
