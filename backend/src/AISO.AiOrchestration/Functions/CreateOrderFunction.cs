using System.Text.Json;
using AISO.Domain.Users;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

/// <summary>
/// Prepare create-sales-order form (confirm card). SAP call runs on Adaptive Card
/// <c>create_so_confirm</c>. Supports up to 5 line items. Loads SalesArea / ValidCustomer
/// choices from SAP when available.
/// </summary>
public sealed class CreateOrderFunction : IFunction
{
    public const int MaxLineSlots = 5;

    private readonly ISapClient _sap;
    private readonly IUserScopeLookup _scope;
    private readonly ILogger<CreateOrderFunction> _logger;

    public CreateOrderFunction(
        ISapClient sap,
        IUserScopeLookup scope,
        ILogger<CreateOrderFunction> logger)
    {
        _sap = sap;
        _scope = scope;
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
            "sales_org": { "type": "string", "description": "Sales Organization (e.g. 'TV01')." },
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
        var customer = ReadString(parameters, "customer") ?? "10100001";
        var salesOrg = ReadString(parameters, "sales_org");
        var distChannel = ReadString(parameters, "dist_channel") ?? "10";
        var division = ReadString(parameters, "division") ?? "00";
        var currency = ReadString(parameters, "currency") ?? "USD";
        var plant = "1010";
        var unit = "PC";

        var userOrg = await _scope.GetSalesOrgBySapUserAsync(requestingSapUser, ct);
        if (string.IsNullOrWhiteSpace(salesOrg))
            salesOrg = string.IsNullOrWhiteSpace(userOrg) ? "TV01" : userOrg;

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

        var areas = await _sap.GetSalesAreasAsync(ct);
        var customers = await _sap.GetValidCustomersAsync(
            salesOrg: string.IsNullOrWhiteSpace(userOrg) ? null : userOrg,
            top: 100,
            ct: ct);

        // If scoped list empty, fall back to unfiltered sample for the dropdown.
        if (customers.Count == 0)
            customers = await _sap.GetValidCustomersAsync(top: 100, ct: ct);

        var salesAreaChoices = areas
            .Select(a => new ConfirmCreateChoice(a.Label, a.Key))
            .ToList();

        var customerChoices = customers
            .GroupBy(c => c.Customer, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(c => new ConfirmCreateChoice(c.Label, c.Customer.Trim()))
            .ToList();

        var preferredArea = areas.FirstOrDefault(a =>
                string.Equals(a.SalesOrg, salesOrg, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.DistChannel, distChannel, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.Division, division, StringComparison.OrdinalIgnoreCase))
            ?? areas.FirstOrDefault(a =>
                string.Equals(a.SalesOrg, salesOrg, StringComparison.OrdinalIgnoreCase))
            ?? areas.FirstOrDefault();

        if (preferredArea is not null)
        {
            salesOrg = preferredArea.SalesOrg;
            distChannel = preferredArea.DistChannel;
            division = preferredArea.Division;
        }

        if (customerChoices.Count > 0
            && !customerChoices.Any(c => string.Equals(c.Value, customer.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            customer = customerChoices[0].Value;
        }

        _logger.LogInformation(
            "CreateOrder confirm step: customer={Customer} lines={LineCount} areas={AreaCount} customers={CustomerCount} by={User}",
            customer, lines.Count, salesAreaChoices.Count, customerChoices.Count, requestingSapUser);

        return FunctionResult.Ok(new ConfirmCreateOrderResponse(
            Customer: customer.Trim(),
            SalesOrg: salesOrg.Trim().ToUpperInvariant(),
            Currency: currency.Trim().ToUpperInvariant(),
            Plant: plant.Trim(),
            Unit: unit.Trim().ToUpperInvariant(),
            Lines: lines,
            DistChannel: distChannel.Trim().ToUpperInvariant(),
            Division: division.Trim().ToUpperInvariant(),
            SalesAreaChoices: salesAreaChoices,
            CustomerChoices: customerChoices));
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
}

public sealed record ConfirmCreateOrderLine(string Material, decimal Qty);

public sealed record ConfirmCreateChoice(string Title, string Value);

/// <summary>Payload telling the bot to show <c>confirm-create.json</c>.</summary>
public sealed record ConfirmCreateOrderResponse(
    string Customer,
    string SalesOrg,
    string Currency,
    string Plant,
    string Unit,
    IReadOnlyList<ConfirmCreateOrderLine> Lines,
    string DistChannel = "10",
    string Division = "00",
    IReadOnlyList<ConfirmCreateChoice>? SalesAreaChoices = null,
    IReadOnlyList<ConfirmCreateChoice>? CustomerChoices = null)
{
    /// <summary>First line material (tests / legacy callers).</summary>
    public string Material => Lines.Count > 0 ? Lines[0].Material : string.Empty;

    /// <summary>First line qty (tests / legacy callers).</summary>
    public decimal Qty => Lines.Count > 0 ? Lines[0].Qty : 1m;

    public string SalesAreaKey => $"{SalesOrg}|{DistChannel}|{Division}";

    public bool UseSalesAreaChoices => SalesAreaChoices is { Count: > 0 };
    public bool UseCustomerChoices => CustomerChoices is { Count: > 0 };
}
