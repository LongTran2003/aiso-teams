using System.Globalization;
using System.Text.Json;
using AISO.Domain.Kpi;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

public sealed class GetKpiByProductFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly ILogger<GetKpiByProductFunction> _logger;

    public GetKpiByProductFunction(ISapClient sap, ILogger<GetKpiByProductFunction> logger)
    {
        _sap = sap;
        _logger = logger;
    }

    public string Name => "GetKpiByProduct";

    public string Description =>
        "Get KPI breakdown per product/material from SAP: revenue, quantity sold, order count. " +
        "Trigger when user asks about top products, best-selling materials, or product-level revenue. " +
        "Returns products ranked by revenue descending.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "materialIdOrName": {
              "type": "string",
              "description": "Material ID or partial name to filter to a specific product."
            },
            "fromDate": { "type": "string", "format": "date" },
            "toDate":   { "type": "string", "format": "date" },
            "salesOrg": {
              "type": "string",
              "enum": ["UE00", "UW00", "DN00", "DS00"]
            },
            "top": { "type": "integer", "default": 10, "minimum": 1, "maximum": 50,
                     "description": "Number of top products to return, ordered by revenue." }
          },
          "additionalProperties": false
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var query = new KpiByProductQuery
        {
            MaterialIdOrName = GetString(parameters, "materialIdOrName"),
            FromDate = GetDate(parameters, "fromDate"),
            ToDate = GetDate(parameters, "toDate"),
            SalesOrg = GetString(parameters, "salesOrg"),
            Top = GetInt(parameters, "top") ?? 10
        };

        _logger.LogInformation(
            "Executing GetKpiByProduct: material={Material}, from={From}, to={To}, top={Top}",
            query.MaterialIdOrName, query.FromDate, query.ToDate, query.Top);

        var products = await _sap.GetKpiByProductAsync(query, ct);

        // QuickChart doughnut chart for top-5 products by revenue
        string? chartUrl = null;
        if (products.Any())
        {
            var top5 = products.Take(5).ToList();
            var labels = string.Join(",", top5.Select(p => $"'{p.MaterialName}'"));
            var data   = string.Join(",", top5.Select(p => p.Revenue));
            var chartConfig = "{\"type\":\"doughnut\",\"data\":{\"labels\":[" + labels + "],\"datasets\":[{\"data\":[" + data + "]}]},\"options\":{\"plugins\":{\"title\":{\"display\":true,\"text\":\"Top Products by Revenue\"}}}}";
            chartUrl = $"https://quickchart.io/chart?c={Uri.EscapeDataString(chartConfig)}";
        }

        return FunctionResult.Ok(new GetKpiByProductResponse(products, chartUrl));
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static int? GetInt(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;

    private static DateOnly? GetDate(JsonElement el, string name)
    {
        var s = GetString(el, name);
        return s is not null && DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}

public record GetKpiByProductResponse(IReadOnlyList<KpiByProduct> Products, string? ChartUrl);
