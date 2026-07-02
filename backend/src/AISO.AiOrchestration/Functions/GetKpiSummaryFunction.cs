using System.Globalization;
using System.Text.Json;
using AISO.Domain.Kpi;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

public sealed class GetKpiSummaryFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly ILogger<GetKpiSummaryFunction> _logger;

    public GetKpiSummaryFunction(ISapClient sap, ILogger<GetKpiSummaryFunction> logger)
    {
        _sap = sap;
        _logger = logger;
    }

    public string Name => "GetKpiSummary";

    public string Description =>
        "Get aggregated KPI dashboard from SAP: total revenue, order count, fulfillment rate, " +
        "cancellation rate. Trigger when user asks about overall performance, KPI overview, " +
        "revenue totals, or sales dashboard. All params optional.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "fromDate": { "type": "string", "format": "date", "description": "Start date YYYY-MM-DD." },
            "toDate":   { "type": "string", "format": "date", "description": "End date YYYY-MM-DD." },
            "salesOrg": {
              "type": "string",
              "enum": ["UE00", "UW00", "DN00", "DS00"],
              "description": "Sales org filter."
            },
            "granularity": {
              "type": "string",
              "enum": ["daily", "weekly", "monthly"],
              "description": "Time breakdown granularity."
            }
          },
          "additionalProperties": false
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var query = new KpiSummaryQuery
        {
            FromDate = GetDate(parameters, "fromDate"),
            ToDate = GetDate(parameters, "toDate"),
            SalesOrg = GetString(parameters, "salesOrg"),
            Granularity = GetString(parameters, "granularity")
        };

        _logger.LogInformation(
            "Executing GetKpiSummary: from={From}, to={To}, salesOrg={SalesOrg}, granularity={Granularity}",
            query.FromDate, query.ToDate, query.SalesOrg, query.Granularity);

        var summary = await _sap.GetKpiSummaryAsync(query, ct);

        // Build QuickChart bar chart for revenue time series
        string? chartUrl = null;
        if (summary.RevenueTimeSeries.Count > 0)
        {
            var labels = string.Join(",", summary.RevenueTimeSeries.Select(p => $"'{p.Label}'"));
            var data   = string.Join(",", summary.RevenueTimeSeries.Select(p => p.Value));
            var chartConfig = "{\"type\":\"bar\",\"data\":{\"labels\":[" + labels + "],\"datasets\":[{\"label\":\"Revenue\",\"data\":[" + data + "],\"backgroundColor\":\"rgba(54,162,235,0.7)\"}]},\"options\":{\"plugins\":{\"title\":{\"display\":true,\"text\":\"Revenue Trend\"}}}}";
            chartUrl = $"https://quickchart.io/chart?c={Uri.EscapeDataString(chartConfig)}";
        }

        return FunctionResult.Ok(new GetKpiSummaryResponse(summary, chartUrl));
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static DateOnly? GetDate(JsonElement el, string name)
    {
        var s = GetString(el, name);
        return s is not null && DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}

public record GetKpiSummaryResponse(KpiSummary Summary, string? ChartUrl);
