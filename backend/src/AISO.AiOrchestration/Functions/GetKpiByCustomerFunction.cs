using System.Globalization;
using System.Text.Json;
using AISO.Domain.Kpi;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

public sealed class GetKpiByCustomerFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly ILogger<GetKpiByCustomerFunction> _logger;

    public GetKpiByCustomerFunction(ISapClient sap, ILogger<GetKpiByCustomerFunction> logger)
    {
        _sap = sap;
        _logger = logger;
    }

    public string Name => "GetKpiByCustomer";

    public string Description =>
        "Get KPI breakdown per customer from SAP: revenue, order count, fulfillment rate. " +
        "Trigger when user asks about top customers, customer revenue, or customer-level performance. " +
        "Returns customers ranked by revenue descending.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "customerIdOrName": {
              "type": "string",
              "description": "Customer ID or partial name to filter to a specific customer."
            },
            "fromDate": { "type": "string", "format": "date" },
            "toDate":   { "type": "string", "format": "date" },
            "salesOrg": {
              "type": "string",
              "enum": ["UE00", "UW00", "DN00", "DS00"]
            },
            "top": { "type": "integer", "default": 10, "minimum": 1, "maximum": 50,
                     "description": "Number of top customers to return, ordered by revenue." }
          },
          "additionalProperties": false
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var query = new KpiByCustomerQuery
        {
            CustomerIdOrName = GetString(parameters, "customerIdOrName"),
            FromDate = GetDate(parameters, "fromDate"),
            ToDate = GetDate(parameters, "toDate"),
            SalesOrg = GetString(parameters, "salesOrg"),
            Top = GetInt(parameters, "top") ?? 10
        };

        _logger.LogInformation(
            "Executing GetKpiByCustomer: customer={Customer}, from={From}, to={To}, top={Top}",
            query.CustomerIdOrName, query.FromDate, query.ToDate, query.Top);

        var customers = await _sap.GetKpiByCustomerAsync(query, ct);

        // QuickChart horizontal bar chart
        string? chartUrl = null;
        if (customers.Any())
        {
            var top5 = customers.Take(5).ToList();
            var labels = string.Join(",", top5.Select(c => $"'{c.CustomerName}'"));
            var data   = string.Join(",", top5.Select(c => c.Revenue));
            var chartConfig = "{\"type\":\"horizontalBar\",\"data\":{\"labels\":[" + labels + "],\"datasets\":[{\"label\":\"Revenue\",\"data\":[" + data + "],\"backgroundColor\":\"rgba(75,192,192,0.7)\"}]},\"options\":{\"plugins\":{\"title\":{\"display\":true,\"text\":\"Top Customers by Revenue\"}}}}";
            chartUrl = $"https://quickchart.io/chart?c={Uri.EscapeDataString(chartConfig)}";
        }

        return FunctionResult.Ok(new GetKpiByCustomerResponse(customers, chartUrl));
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

public record GetKpiByCustomerResponse(IReadOnlyList<KpiByCustomer> Customers, string? ChartUrl);
