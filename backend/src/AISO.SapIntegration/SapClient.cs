using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AISO.Domain.SalesOrders;
using Microsoft.Extensions.Logging;

namespace AISO.SapIntegration;

public class SapClient : ISapClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SapClient> _logger;

    public SapClient(HttpClient httpClient, ILogger<SapClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SalesOrder>> GetSalesOrdersAsync(SalesOrdersQuery query, CancellationToken ct = default)
    {
        var urlBuilder = new StringBuilder("SalesOrder?sap-client=324&$format=json");
        
        var filters = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(query.CustomerIdOrName))
        {
            filters.Add($"SoldToParty eq '{query.CustomerIdOrName}'");
        }
        
        if (!string.IsNullOrWhiteSpace(query.SalesOrg))
        {
            filters.Add($"SalesOrganization eq '{query.SalesOrg}'");
        }

        if (query.FromDate.HasValue)
        {
            filters.Add($"SalesOrderDate ge {query.FromDate.Value:yyyy-MM-dd}");
        }

        if (query.ToDate.HasValue)
        {
            filters.Add($"SalesOrderDate le {query.ToDate.Value:yyyy-MM-dd}");
        }
        
        // Status mapping (approximated for demo purposes)
        if (query.Status.HasValue)
        {
            // OData logic depends on SAP fields (e.g., OverallDeliveryStatus, OverallSDProcessStatus)
            // Example mapping:
            // if (query.Status == SalesOrderStatus.Open) filters.Add("OverallSDProcessStatus eq 'A'");
            // We will omit strict status filtering for now since SAP schema details are missing
        }

        if (filters.Any())
        {
            urlBuilder.Append("&$filter=").Append(Uri.EscapeDataString(string.Join(" and ", filters)));
        }

        urlBuilder.Append($"&$top={query.Top}");

        var url = urlBuilder.ToString();
        _logger.LogInformation("Calling SAP OData: {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var rawJson = await response.Content.ReadAsStringAsync(ct);
            _logger.LogInformation("SAP Raw Response: {RawJson}", rawJson);

            var result = JsonSerializer.Deserialize<ODataResponse<SapSalesOrderDto>>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (result?.Value == null)
                return Array.Empty<SalesOrder>();

            return result.Value.Select(MapToDomain).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SAP OData GetSalesOrdersAsync");
            throw;
        }
    }

    public async Task<SalesOrder?> GetSalesOrderByIdAsync(string soNumber, CancellationToken ct = default)
    {
        var url = $"SalesOrder('{soNumber}')?sap-client=324&$format=json";
        _logger.LogInformation("Calling SAP OData: {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                
                response.EnsureSuccessStatusCode();
            }

            var dto = await response.Content.ReadFromJsonAsync<SapSalesOrderDto>(cancellationToken: ct);
            return dto == null ? null : MapToDomain(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SAP OData GetSalesOrderByIdAsync for {SoNumber}", soNumber);
            throw;
        }
    }

    private SalesOrder MapToDomain(SapSalesOrderDto dto)
    {
        return new SalesOrder
        {
            SoNumber = string.IsNullOrEmpty(dto.SoNumber) ? "UNKNOWN" : dto.SoNumber,
            CustomerId = dto.Customer ?? string.Empty,
            CustomerName = dto.Customer ?? "Unknown Customer",
            OrderDate = DateOnly.TryParse(dto.DocDate, out var date) ? date : DateOnly.MinValue,
            NetValue = dto.NetValue ?? 0,
            Currency = string.IsNullOrEmpty(dto.Currency) ? "USD" : dto.Currency,
            Status = MapStatus(dto.OverallStatus),
            SalesOrg = dto.SalesOrg ?? "UNKNOWN",
            Items = Array.Empty<SalesOrderItem>()
        };
    }

    private SalesOrderStatus MapStatus(string? sapStatus)
    {
        return sapStatus switch
        {
            "A" => SalesOrderStatus.Open,
            "B" => SalesOrderStatus.PartiallyDelivered,
            "C" => SalesOrderStatus.Delivered,
            _ => SalesOrderStatus.Open
        };
    }

    private class ODataResponse<T>
    {
        [JsonPropertyName("value")]
        public List<T>? Value { get; set; }
    }

    private class SapSalesOrderDto
    {
        public string? SoNumber { get; set; }
        public string? DocType { get; set; }
        public string? Customer { get; set; }
        public string? SalesOrg { get; set; }
        public string? DistChannel { get; set; }
        public string? Division { get; set; }
        public string? Currency { get; set; }
        public decimal? NetValue { get; set; }
        public string? DocDate { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedDate { get; set; }
        public string? OverallStatus { get; set; }
    }
}
