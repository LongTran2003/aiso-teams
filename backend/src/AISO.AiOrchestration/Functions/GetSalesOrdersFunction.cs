using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AISO.Domain.SalesOrders;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging;

namespace AISO.AiOrchestration.Functions;

public sealed class GetSalesOrdersFunction : IFunction
{
    private readonly ISapClient _sap;
    private readonly ILogger<GetSalesOrdersFunction> _logger;

    // Characters that can cause OData query issues
    private static readonly Regex ProblematicCharsRegex = new(
        @"[%'""\[\];\\]|--|/\*|\*/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Pattern for valid SAP customer IDs
    private static readonly Regex ValidCustomerIdPattern = new(
        @"^[A-Z0-9][A-Z0-9_-]{0,11}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public GetSalesOrdersFunction(
        ISapClient sap,
        ILogger<GetSalesOrdersFunction> logger)
    {
        _sap = sap;
        _logger = logger;
    }

    public string Name => "GetSalesOrders";

    public string Description =>
        "Retrieve sales orders. Supports filtering by customer (ID or partial name), " +
        "sales organization, date range, status, and ownership. " +
        "For 'my sales orders' / 'đơn của tôi' / 'đơn hàng của tôi', set ownedByMe=true " +
        "(filters OwnerSapUser to the requesting SAP user). " +
        "For 'recent orders' / 'show open orders' / 'all open orders' without 'my', leave ownedByMe unset.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "customerIdOrName": {
              "type": "string",
              "description": "Customer ID (exact eq on Customer, e.g. '1000') or partial customer name (contains on CustomerName, e.g. 'Philly Bikes'). Do NOT include time modifiers like 'recent', 'gần đây', 'mới nhất' as part of the customer name."
            },
            "salesOrg": {
              "type": "string",
              "enum": ["TV01", "FU24", "UE00", "UW00", "DN00", "DS00"],
              "description": "Sales organization code."
            },
            "fromDate": { "type": "string", "format": "date" },
            "toDate":   { "type": "string", "format": "date" },
            "status": {
              "type": "string",
              "enum": ["Open", "Blocked", "PartiallyDelivered", "Delivered", "Invoiced", "Cancelled"]
            },
            "ownedByMe": {
              "type": "boolean",
              "description": "When true, only return orders owned by the requesting SAP user (OwnerSapUser). Use for 'my orders' / 'của tôi'."
            },
            "top": { "type": "integer", "default": 10, "minimum": 1, "maximum": 50 }
          },
          "additionalProperties": false
        }
        """;

    public async Task<FunctionResult> ExecuteAsync(JsonElement parameters, string requestingSapUser, CancellationToken ct = default)
    {
        var ownedByMe = GetBool(parameters, "ownedByMe")
                     ?? GetBool(parameters, "mine_only")
                     ?? GetBool(parameters, "owned_by_me");

        // Sanitize customerIdOrName to prevent OData injection
        var rawCustomerIdOrName = GetString(parameters, "customerIdOrName")
                               ?? GetString(parameters, "customer_id");
        var sanitizedCustomerIdOrName = SanitizeCustomerIdOrName(rawCustomerIdOrName);

        // Validate dates before passing to SAP
        var fromDate = GetDate(parameters, "fromDate")
                    ?? GetDate(parameters, "date_from");
        var toDate = GetDate(parameters, "toDate")
                  ?? GetDate(parameters, "date_to");

        // Validate date range
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            _logger.LogWarning(
                "Invalid date range: fromDate {FromDate} is after toDate {ToDate}. Swapping dates.",
                fromDate.Value, toDate.Value);
            (fromDate, toDate) = (toDate, fromDate);
        }

        var query = new SalesOrdersQuery
        {
            CustomerIdOrName = sanitizedCustomerIdOrName,
            SalesOrg = GetString(parameters, "salesOrg"),
            FromDate = fromDate,
            ToDate = toDate,
            Status = GetEnum<SalesOrderStatus>(parameters, "status"),
            OwnerSapUser = ownedByMe == true
                ? requestingSapUser
                : GetString(parameters, "ownerSapUser") ?? GetString(parameters, "owner_sap_user"),
            Top = GetInt(parameters, "top") ?? 10
        };

        // Guard: for "my orders" / "đơn của tôi" we MUST have a real SAP user,
        // otherwise the SAP filter is meaningless and may return confusing errors.
        if (ownedByMe == true && string.IsNullOrWhiteSpace(requestingSapUser))
        {
            const string msg = "Your Teams account is not linked to a SAP user yet. " +
                               "Say \"hi\" or \"link\" to connect your SAP account, then retry.";
            _logger.LogWarning("getSalesOrders: ownedByMe=true but requestingSapUser is empty");

            return FunctionResult.Fail(msg);
        }

        _logger.LogInformation(
            "Executing getSalesOrders: customer={Customer}, salesOrg={SalesOrg}, " +
            "from={FromDate}, to={ToDate}, status={Status}, owner={Owner}, top={Top}",
            query.CustomerIdOrName, query.SalesOrg, query.FromDate, query.ToDate,
            query.Status, query.OwnerSapUser, query.Top);

        try
        {
            var orders = await _sap.GetSalesOrdersAsync(query, ct);

            _logger.LogInformation(
                "getSalesOrders returned {Count} orders", orders.Count);

            string? chartUrl = null;
            if (orders.Any())
            {
                var statusCounts = orders.GroupBy(o => o.Status)
                                         .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                                         .ToList();

                var labels = string.Join(",", statusCounts.Select(x => $"'{x.Status}'"));
                var data = string.Join(",", statusCounts.Select(x => x.Count));

                var chartConfig = $$"""
                {
                  type: 'doughnut',
                  data: {
                    labels: [{{labels}}],
                    datasets: [{
                      data: [{{data}}]
                    }]
                  },
                  options: {
                    plugins: {
                      title: { display: true, text: 'Order Status Distribution' },
                      datalabels: { display: true, color: '#fff', font: { weight: 'bold' } }
                    }
                  }
                }
                """;

                chartConfig = System.Text.RegularExpressions.Regex.Replace(chartConfig, @"\s+", "");
                chartUrl = $"https://quickchart.io/chart?c={Uri.EscapeDataString(chartConfig)}";
            }

            var title = ownedByMe == true ? "My sales orders" : "Sales orders";
            var response = new GetSalesOrdersResponse(orders, chartUrl, title);

            return FunctionResult.Ok(response);
        }
        catch (SapODataException ex) when (ex.HttpStatusCode == 400)
        {
            _logger.LogWarning(ex,
                "SAP OData 400 error for GetSalesOrders. CustomerIdOrName={Customer}",
                sanitizedCustomerIdOrName);

            // Return empty result with helpful message instead of failing
            var emptyResponse = new GetSalesOrdersResponse(
                Array.Empty<SalesOrder>(),
                null,
                ownedByMe == true ? "My sales orders" : "Sales orders");

            return FunctionResult.Ok(emptyResponse);
        }
        catch (SapODataException ex) when (ex.HttpStatusCode >= 500)
        {
            // 5xx SAP error → user-friendly message, NOT the raw SAP stack trace.
            _logger.LogError(ex,
                "SAP backend error {Status} for GetSalesOrders. Owner={Owner}",
                ex.HttpStatusCode, query.OwnerSapUser);

            return FunctionResult.Fail(
                $"SAP returned {ex.HttpStatusCode}. Please try again or contact support if it persists.");
        }
        catch (OperationCanceledException)
        {
            // Turn timeout or caller cancellation. Surface a clear retry hint
            // instead of the raw "The operation was canceled" text.
            _logger.LogWarning(
                "getSalesOrders canceled by timeout/caller for owner={Owner}",
                query.OwnerSapUser);

            return FunctionResult.Fail(
                "The request took too long and was canceled. " +
                "Try narrowing the filter (a smaller date range, a specific sales org) " +
                "or retry in a moment.");
        }
        catch (HttpRequestException ex)
        {
            // SAP endpoint refused / DNS / TLS — common with real SAP when network is flaky.
            _logger.LogError(ex, "Network error calling SAP for GetSalesOrders");

            return FunctionResult.Fail(
                "Could not reach SAP. Check your connection and try again.");
        }
    }

    /// <summary>
    /// Sanitizes customer ID or name input to prevent OData injection attacks
    /// and remove characters that could cause SAP OData 400 errors.
    /// </summary>
    private static string? SanitizeCustomerIdOrName(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.Trim();

        // If it looks like a customer ID (alphanumeric with specific pattern), validate it
        if (IsLikelyCustomerId(trimmed))
        {
            // Allow only valid customer ID characters
            if (!ValidCustomerIdPattern.IsMatch(trimmed))
            {
                // Invalid format - treat as null to avoid SAP errors
                return null;
            }
            return trimmed.ToUpperInvariant();
        }

        // For customer names (contains letters or spaces), sanitize but preserve readability
        // Remove OData dangerous characters but keep common name characters
        var sanitized = ProblematicCharsRegex.Replace(trimmed, "");

        // Trim excessive whitespace
        sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();

        // Limit length to prevent overly long queries
        if (sanitized.Length > 100)
        {
            sanitized = sanitized[..100];
        }

        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    /// <summary>
    /// Determines if the input is likely a customer ID (numeric or short alphanumeric)
    /// rather than a customer name (contains spaces or longer mixed text).
    /// </summary>
    private static bool IsLikelyCustomerId(string input)
    {
        // Customer IDs are typically:
        // - All digits (e.g., "1000", "12345")
        // - Short alphanumeric codes (e.g., "USCU_001", "CUST-123")
        if (string.IsNullOrWhiteSpace(input) || input.Length > 15)
            return false;

        // All digits = definitely customer ID
        if (input.All(char.IsDigit))
            return true;

        // Contains spaces = definitely a name, not an ID
        if (input.Contains(' '))
            return false;

        // Short alphanumeric without spaces could be either
        // Be conservative: if it's short and alphanumeric, allow it through validation
        return input.Length <= 12 && input.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static bool? GetBool(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return null;
        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(p.GetString(), out var b) => b,
            _ => null
        };
    }

    private static int? GetInt(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
            ? p.GetInt32()
            : null;

    private static DateOnly? GetDate(JsonElement el, string name)
    {
        var s = GetString(el, name);
        return s is not null
               && DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
    }

    private static T? GetEnum<T>(JsonElement el, string name) where T : struct, Enum
    {
        var s = GetString(el, name);
        return s is not null && Enum.TryParse<T>(s, ignoreCase: true, out var v) ? v : null;
    }
}

public record GetSalesOrdersResponse(
    IReadOnlyList<SalesOrder> Orders,
    string? ChartUrl,
    string Title = "Sales orders");
