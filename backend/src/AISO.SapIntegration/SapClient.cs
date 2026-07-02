using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AISO.Domain.Kpi;
using AISO.Domain.SalesOrders;
using AISO.SapIntegration.Dtos;
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
            builder.Filter("Customer", "eq", query.CustomerIdOrName);
        }

        if (!string.IsNullOrWhiteSpace(query.SalesOrg))
        {
            builder.Filter("SalesOrg", "eq", query.SalesOrg);
        }

        if (query.FromDate.HasValue)
        {
            builder.FilterRaw($"DocDate ge {query.FromDate.Value:yyyy-MM-dd}");
        }

        if (query.ToDate.HasValue)
        {
            builder.FilterRaw($"DocDate le {query.ToDate.Value:yyyy-MM-dd}");
        }

        // Note: The SalesOrder view in this SAP OData V4 service is flat and does not support Expand("ITEMS")

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
            ITEMS = dto.Items.Select((i, index) => new
            {
                SO_NUMBER = "0000000000",
                ITEM_NO = ((index + 1) * 10).ToString().PadLeft(6, '0'),
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
            REASON_CODE = reason
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
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        var jsonString = System.Text.Json.JsonSerializer.Serialize(payload);
        var stringContent = new StringContent(jsonString, System.Text.Encoding.UTF8, "application/json");
        stringContent.Headers.ContentType.CharSet = string.Empty;
        request.Content = stringContent;
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

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
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {errorBody}");
        }

        return await response.Content.ReadFromJsonAsync<TResult>(cancellationToken: ct);
    }

    // -----------------------------------------------------------------------
    // KPI methods
    // -----------------------------------------------------------------------

    public async Task<KpiSummary> GetKpiSummaryAsync(KpiSummaryQuery query, CancellationToken ct = default)
    {
        // Entity: ZI_AISO_KPI_SUMMARY (to be created by SAP team)
        // Falls back to aggregating from SalesOrder if KPI view not yet ready.
        var builder = new ODataQueryBuilder("ZI_AISO_KPI_SUMMARY")
            .AddCustomParam("sap-client", "324");

        if (query.FromDate.HasValue)
            builder.FilterRaw($"DocDate ge {query.FromDate.Value:yyyy-MM-dd}");
        if (query.ToDate.HasValue)
            builder.FilterRaw($"DocDate le {query.ToDate.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(query.SalesOrg))
            builder.Filter("SalesOrg", "eq", query.SalesOrg);

        var url = builder.Build();
        _logger.LogInformation("Calling SAP OData (KPI Summary): {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            
            // Fallback: If view is missing, aggregate SalesOrders
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("SAP KPI view not found, falling back to manual aggregation");
                return await GetKpiSummaryFallbackAsync(query, ct);
            }
            
            response.EnsureSuccessStatusCode();

            var rawJson = await response.Content.ReadAsStringAsync(ct);
            _logger.LogDebug("SAP KPI Summary raw: {Raw}", rawJson);

            var result = JsonSerializer.Deserialize<ODataResponse<SapKpiSummaryDto>>(
                rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.Value == null || result.Value.Count == 0)
                return new KpiSummary { Period = BuildPeriodLabel(query.FromDate, query.ToDate), SalesOrg = query.SalesOrg, Granularity = query.Granularity };

            // Aggregate all rows into a single summary
            var rows = result.Value;
            var totalRevenue = rows.Sum(r => r.TotalRevenue ?? 0);
            var totalOrders = rows.Sum(r => r.OrderCount ?? 0);
            var deliveredOrders = rows.Sum(r => r.DeliveredCount ?? 0);
            var openOrders = rows.Sum(r => r.OpenCount ?? 0);
            var overdueOrders = rows.Sum(r => r.OverdueCount ?? 0);
            var fulfillmentRate = totalOrders > 0 ? (decimal)deliveredOrders / totalOrders * 100 : 0;
            var cancelledOrders = rows.Sum(r => r.CancelledCount ?? 0);
            var cancellationRate = totalOrders > 0 ? (decimal)cancelledOrders / totalOrders * 100 : 0;

            var timeSeries = rows
                .Where(r => !string.IsNullOrEmpty(r.PeriodLabel))
                .Select(r => new KpiDataPoint(r.PeriodLabel!, r.TotalRevenue ?? 0))
                .ToList();

            return new KpiSummary
            {
                TotalRevenue = totalRevenue,
                Currency = rows.FirstOrDefault()?.Currency ?? "USD",
                TotalOrders = totalOrders,
                OpenOrders = openOrders,
                DeliveredOrders = deliveredOrders,
                OverdueOrders = overdueOrders,
                FulfillmentRate = Math.Round(fulfillmentRate, 1),
                CancellationRate = Math.Round(cancellationRate, 1),
                Period = BuildPeriodLabel(query.FromDate, query.ToDate),
                SalesOrg = query.SalesOrg,
                Granularity = query.Granularity,
                RevenueTimeSeries = timeSeries
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching KPI summary from SAP");
            throw;
        }
    }

    private async Task<KpiSummary> GetKpiSummaryFallbackAsync(KpiSummaryQuery query, CancellationToken ct)
    {
        // Query maximum possible orders within range
        var orders = await GetSalesOrdersAsync(new SalesOrdersQuery
        {
            FromDate = query.FromDate,
            ToDate = query.ToDate,
            SalesOrg = query.SalesOrg,
            Top = 500 // Aggregate up to 500 recent orders
        }, ct);
        
        var totalOrders = orders.Count;
        var deliveredOrders = orders.Count(o => o.Status == SalesOrderStatus.Delivered);
        var openOrders = orders.Count(o => o.Status == SalesOrderStatus.Open);
        // Note: For cancelled/overdue we'd need more status mappings, simplified here
        
        return new KpiSummary
        {
            TotalRevenue = orders.Sum(o => o.NetValue),
            Currency = orders.FirstOrDefault()?.Currency ?? "USD",
            TotalOrders = totalOrders,
            OpenOrders = openOrders,
            DeliveredOrders = deliveredOrders,
            FulfillmentRate = totalOrders > 0 ? Math.Round((decimal)deliveredOrders / totalOrders * 100, 1) : 0,
            Period = BuildPeriodLabel(query.FromDate, query.ToDate),
            SalesOrg = query.SalesOrg,
            Granularity = query.Granularity,
            RevenueTimeSeries = new List<KpiDataPoint>()
        };
    }

    public async Task<IReadOnlyList<KpiByCustomer>> GetKpiByCustomerAsync(KpiByCustomerQuery query, CancellationToken ct = default)
    {
        // Entity: ZI_AISO_KPI_BY_CUSTOMER (to be created by SAP team)
        var builder = new ODataQueryBuilder("ZI_AISO_KPI_BY_CUSTOMER")
            .AddCustomParam("sap-client", "324")
            .Top(query.Top);

        if (!string.IsNullOrWhiteSpace(query.CustomerIdOrName))
            builder.Filter("Customer", "eq", query.CustomerIdOrName);
        if (query.FromDate.HasValue)
            builder.FilterRaw($"DocDate ge {query.FromDate.Value:yyyy-MM-dd}");
        if (query.ToDate.HasValue)
            builder.FilterRaw($"DocDate le {query.ToDate.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(query.SalesOrg))
            builder.Filter("SalesOrg", "eq", query.SalesOrg);

        var url = builder.Build();
        _logger.LogInformation("Calling SAP OData (KPI By Customer): {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var rawJson = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ODataResponse<SapKpiByCustomerDto>>(
                rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.Value == null) return Array.Empty<KpiByCustomer>();

            return result.Value.Select(r => new KpiByCustomer
            {
                CustomerId = r.Customer ?? string.Empty,
                CustomerName = r.CustomerName ?? r.Customer ?? string.Empty,
                Revenue = r.TotalRevenue ?? 0,
                Currency = r.Currency ?? "USD",
                OrderCount = r.OrderCount ?? 0,
                FulfillmentRate = r.FulfillmentRate ?? 0
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SAP KPI By Customer endpoint");
            throw;
        }
    }

    public async Task<IReadOnlyList<KpiByProduct>> GetKpiByProductAsync(KpiByProductQuery query, CancellationToken ct = default)
    {
        // Entity: ZI_AISO_KPI_BY_PRODUCT (to be created by SAP team)
        var builder = new ODataQueryBuilder("ZI_AISO_KPI_BY_PRODUCT")
            .AddCustomParam("sap-client", "324")
            .Top(query.Top);

        if (!string.IsNullOrWhiteSpace(query.MaterialIdOrName))
            builder.Filter("Material", "eq", query.MaterialIdOrName);
        if (query.FromDate.HasValue)
            builder.FilterRaw($"DocDate ge {query.FromDate.Value:yyyy-MM-dd}");
        if (query.ToDate.HasValue)
            builder.FilterRaw($"DocDate le {query.ToDate.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(query.SalesOrg))
            builder.Filter("SalesOrg", "eq", query.SalesOrg);

        var url = builder.Build();
        _logger.LogInformation("Calling SAP OData (KPI By Product): {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var rawJson = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ODataResponse<SapKpiByProductDto>>(
                rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.Value == null) return Array.Empty<KpiByProduct>();

            return result.Value.Select(r => new KpiByProduct
            {
                MaterialId = r.Material ?? string.Empty,
                MaterialName = r.MaterialName ?? r.Material ?? string.Empty,
                Revenue = r.TotalRevenue ?? 0,
                Currency = r.Currency ?? "USD",
                TotalQty = r.TotalQty ?? 0,
                Unit = r.Unit ?? "PC",
                OrderCount = r.OrderCount ?? 0
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SAP KPI By Product endpoint");
            throw;
        }
    }

    public async Task<IReadOnlyList<OverdueOrder>> GetOverdueOrdersAsync(OverdueOrdersQuery query, CancellationToken ct = default)
    {
        // Entity: ZI_AISO_KPI_SO_AGING (already exists in SAP team's code)
        var builder = new ODataQueryBuilder("ZI_AISO_KPI_SO_AGING")
            .AddCustomParam("sap-client", "324")
            .Top(query.Top);

        if (!string.IsNullOrWhiteSpace(query.CustomerIdOrName))
            builder.Filter("Customer", "eq", query.CustomerIdOrName);
        if (!string.IsNullOrWhiteSpace(query.SalesOrg))
            builder.Filter("SalesOrg", "eq", query.SalesOrg);
        if (query.DaysPastDue.HasValue)
            builder.FilterRaw($"DaysPastDue ge {query.DaysPastDue.Value}");

        // Always filter for actually overdue (DaysPastDue > 0)
        builder.FilterRaw("DaysPastDue gt 0");

        var url = builder.Build();
        _logger.LogInformation("Calling SAP OData (Overdue Orders): {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var rawJson = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ODataResponse<SapOverdueOrderDto>>(
                rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.Value == null) return Array.Empty<OverdueOrder>();

            return result.Value.Select(r => new OverdueOrder
            {
                SoNumber = r.SoNumber ?? string.Empty,
                CustomerId = r.Customer ?? string.Empty,
                CustomerName = r.CustomerName ?? r.Customer ?? string.Empty,
                ScheduledDeliveryDate = DateOnly.TryParse(r.ScheduledDeliveryDate, out var d) ? d : DateOnly.MinValue,
                DaysPastDue = r.DaysPastDue ?? 0,
                NetValue = r.NetValue ?? 0,
                Currency = r.Currency ?? "USD",
                SalesOrg = r.SalesOrg ?? string.Empty
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SAP Overdue Orders endpoint");
            throw;
        }
    }

    private static string BuildPeriodLabel(DateOnly? from, DateOnly? to)
    {
        if (from.HasValue && to.HasValue)
            return $"{from.Value:yyyy-MM-dd} to {to.Value:yyyy-MM-dd}";
        if (from.HasValue)
            return $"From {from.Value:yyyy-MM-dd}";
        return "All time";
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
}

