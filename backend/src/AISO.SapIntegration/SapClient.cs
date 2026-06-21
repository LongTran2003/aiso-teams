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
    private readonly ISapTokenManager _tokenManager;
    private readonly ILogger<SapClient> _logger;

    public SapClient(HttpClient httpClient, ISapTokenManager tokenManager, ILogger<SapClient> logger)
    {
        _httpClient = httpClient;
        _tokenManager = tokenManager;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SalesOrder>> GetSalesOrdersAsync(SalesOrdersQuery query, CancellationToken ct = default)
    {
        var builder = new ODataQueryBuilder("SalesOrder")
            .AddCustomParam("sap-client", "324")
            .Top(query.Top);

        if (!string.IsNullOrWhiteSpace(query.CustomerIdOrName))
        {
            builder.Filter("SoldToParty", "eq", query.CustomerIdOrName);
        }

        if (!string.IsNullOrWhiteSpace(query.SalesOrg))
        {
            builder.Filter("SalesOrganization", "eq", query.SalesOrg);
        }

        if (query.FromDate.HasValue)
        {
            builder.FilterRaw($"SalesOrderDate ge {query.FromDate.Value:yyyy-MM-dd}");
        }

        if (query.ToDate.HasValue)
        {
            builder.FilterRaw($"SalesOrderDate le {query.ToDate.Value:yyyy-MM-dd}");
        }

        // We expand ITEMS by default to map domain items if available
        builder.Expand("ITEMS");

        var url = builder.Build();
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

    public async Task<SalesOrder> CreateSalesOrderAsync(CreateSalesOrderDto dto, CancellationToken ct = default)
    {
        var url = "SalesOrder/com.sap.gateway.srvd_a2x.zsd_aiso_sales_order.v0001.createSalesOrder?sap-client=324&$format=json";
        _logger.LogInformation("Calling SAP OData: {Url}", url);

        var payload = new
        {
            DOC_TYPE = dto.DocType,
            SALES_ORG = dto.SalesOrg,
            DIST_CHANNEL = dto.DistChannel,
            DIVISION = dto.Division,
            CUSTOMER = dto.Customer,
            CURRENCY = dto.Currency,
            ITEMS = dto.Items.Select(i => new
            {
                MATERIAL = i.Material,
                PLANT = i.Plant,
                ORDER_QTY = i.OrderQty,
                UNIT = i.Unit
            }).ToList()
        };

        try
        {
            var result = await SendPostRequestAsync<SapSalesOrderDto, object>(url, payload, ct);
            return result == null ? throw new InvalidOperationException("Failed to deserialize created order.") : MapToDomain(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SAP OData CreateSalesOrderAsync");
            throw;
        }
    }

    public async Task<SalesOrder> UpdateReferenceAsync(string soNumber, string newReference, string requestingSapUser, CancellationToken ct = default)
    {
        var url = $"SalesOrder('{soNumber}')/com.sap.gateway.srvd_a2x.zsd_aiso_sales_order.v0001.updateReference?sap-client=324&$format=json";
        _logger.LogInformation("Calling SAP OData: {Url}", url);

        var payload = new
        {
            REQUESTING_TEAMS_USER = requestingSapUser,
            NEW_REFERENCE = newReference
        };

        try
        {
            var result = await SendPostRequestAsync<SapSalesOrderDto, object>(url, payload, ct);
            return result == null ? throw new InvalidOperationException("Failed to deserialize updated order.") : MapToDomain(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SAP OData UpdateReferenceAsync for {SoNumber}", soNumber);
            throw;
        }
    }

    public async Task<SalesOrder> CancelOrderAsync(string soNumber, string reason, string requestingSapUser, CancellationToken ct = default)
    {
        var url = $"SalesOrder('{soNumber}')/com.sap.gateway.srvd_a2x.zsd_aiso_sales_order.v0001.cancelOrder?sap-client=324&$format=json";
        _logger.LogInformation("Calling SAP OData: {Url}", url);

        var payload = new
        {
            REQUESTING_TEAMS_USER = requestingSapUser,
            REASON = reason
        };

        try
        {
            var result = await SendPostRequestAsync<SapSalesOrderDto, object>(url, payload, ct);
            return result == null ? throw new InvalidOperationException("Failed to deserialize canceled order.") : MapToDomain(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SAP OData CancelOrderAsync for {SoNumber}", soNumber);
            throw;
        }
    }

    private async Task<TResult?> SendPostRequestAsync<TResult, TPayload>(string url, TPayload payload, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };

        var authContext = await _tokenManager.GetAuthContextAsync(ct);
        if (!string.IsNullOrEmpty(authContext.CsrfToken))
        {
            request.Headers.Add("x-csrf-token", authContext.CsrfToken);
        }

        if (!string.IsNullOrEmpty(authContext.SessionCookie))
        {
            request.Headers.Add("Cookie", authContext.SessionCookie);
        }

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TResult>(cancellationToken: ct);
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
