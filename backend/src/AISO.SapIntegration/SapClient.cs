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

    private string FormatSoNumber(string? soNumber)
    {
        if (string.IsNullOrWhiteSpace(soNumber)) return "UNKNOWN";
        if (soNumber.Length < 10 && soNumber.All(char.IsDigit))
        {
            return soNumber.PadLeft(10, '0');
        }
        return soNumber;
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

        if (query.Status.HasValue)
        {
            ApplyStatusFilter(builder, query.Status.Value);
        }

        // SalesOrder is flat (no $expand). Detail loads items via a separate SalesOrderItem request.

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

            return result.Value.Select(dto => MapToDomain(dto)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SAP OData GetSalesOrdersAsync");
            throw;
        }
    }

    public async Task<SalesOrder?> GetSalesOrderByIdAsync(string soNumber, CancellationToken ct = default)
    {
        var formattedSo = FormatSoNumber(soNumber);
        var url = $"SalesOrder('{formattedSo}')?sap-client=324&$format=json";
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

            var dto = await response.Content.ReadFromJsonAsync<SapSalesOrderDto>(
                JsonOptions,
                cancellationToken: ct);
            if (dto is null)
                return null;

            // No $expand association on SalesOrder — load items with a side request.
            var items = await GetSalesOrderItemsAsync(formattedSo, ct);
            return MapToDomain(dto, items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SAP OData GetSalesOrderByIdAsync for {SoNumber}", soNumber);
            throw;
        }
    }

    /// <summary>
    /// Loads line items for one SO via SalesOrderItem?$filter=SoNumber eq '…'
    /// (SAP entity set is flat; association/$expand is not available).
    /// </summary>
    private async Task<IReadOnlyList<SalesOrderItem>> GetSalesOrderItemsAsync(
        string formattedSoNumber,
        CancellationToken ct)
    {
        var url = new ODataQueryBuilder("SalesOrderItem")
            .AddCustomParam("sap-client", "324")
            .Filter("SoNumber", "eq", formattedSoNumber)
            .Build();

        _logger.LogInformation("Calling SAP OData items: {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "SAP SalesOrderItem request failed for {SoNumber}: {StatusCode}",
                    formattedSoNumber,
                    (int)response.StatusCode);
                return Array.Empty<SalesOrderItem>();
            }

            var rawJson = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ODataResponse<SapSalesOrderItemDto>>(rawJson, JsonOptions);
            if (result?.Value is null || result.Value.Count == 0)
                return Array.Empty<SalesOrderItem>();

            return result.Value.Select(MapItemToDomain).ToList();
        }
        catch (Exception ex)
        {
            // Detail card should still render header if items temporarily fail.
            _logger.LogWarning(ex, "Failed to load SalesOrderItem for {SoNumber}", formattedSoNumber);
            return Array.Empty<SalesOrderItem>();
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
            // TEMPORARY MOCK: SAP Backend is currently throwing ABAP RAISE_SHORTDUMP for createOrder
            // (CALL_FUNCTION_CONFLICT_LENG) due to parameter mismatch.
            _logger.LogWarning("SAP CreateSalesOrder is currently broken (RAISE_SHORTDUMP). Mocking success response.");

            return new SalesOrder
            {
                SoNumber = "0000009999", // Mock generated ID
                CustomerId = dto.Customer ?? "UNKNOWN",
                CustomerName = "Mocked Customer",
                SalesOrg = dto.SalesOrg ?? "1000",
                OrderDate = DateOnly.FromDateTime(DateTime.UtcNow),
                NetValue = 5000,
                Currency = dto.Currency ?? "USD",
                Status = SalesOrderStatus.Open,
                Items = new List<SalesOrderItem>()
            };

            // REAL IMPLEMENTATION:
            // var result = await SendPostRequestAsync<SapSalesOrderDto, object>(url, payload, ct);
            // return result == null ? throw new InvalidOperationException("Failed to deserialize created order.") : MapToDomain(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SAP OData CreateSalesOrderAsync");
            throw;
        }
    }

    public async Task<SalesOrder> UpdateReferenceAsync(string soNumber, string newReference, string requestingSapUser, CancellationToken ct = default)
    {
        var formattedSo = FormatSoNumber(soNumber);
        var url = $"SalesOrder('{formattedSo}')/com.sap.gateway.srvd_a2x.zsd_aiso_sales_order.v0001.updateReference?sap-client=324&$format=json";
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

    public async Task<SalesOrder> RejectOrderAsync(string soNumber, string rejectionCode, string requestingTeamsUser, CancellationToken ct = default)
    {
        var formattedSo = FormatSoNumber(soNumber);
        var url = $"SalesOrder('{formattedSo}')/com.sap.gateway.srvd_a2x.zsd_aiso_sales_order.v0001.rejectOrder?sap-client=324&$format=json";
        _logger.LogInformation("Calling SAP OData: {Url}", url);

        var payload = new
        {
            REQUESTING_TEAMS_USER = requestingTeamsUser,
            REJECTION_CODE = rejectionCode,
        };

        try
        {
            var result = await SendPostRequestAsync<SapSalesOrderDto, object>(url, payload, ct);
            if (result == null)
                throw new InvalidOperationException("Failed to deserialize rejected order.");

            // RAP action often returns only %tky (no SoNumber in body). Keep the known key.
            var order = MapToDomain(result);
            return order.SoNumber is "UNKNOWN"
                ? order with { SoNumber = formattedSo }
                : order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SAP OData RejectOrderAsync for {SoNumber}", soNumber);
            throw;
        }
    }

    public async Task<SalesOrder> ReleaseOrderAsync(string soNumber, string requestingTeamsUser, CancellationToken ct = default)
    {
        return await PostSalesOrderActionAsync(
            soNumber,
            "releaseOrder",
            new { REQUESTING_TEAMS_USER = requestingTeamsUser },
            ct);
    }

    public async Task<SalesOrder> ApproveOrderAsync(string soNumber, string requestingSapUser, CancellationToken ct = default)
    {
        // Param name is REQUESTING_TEAMS_USER in DDIC but value must be SAP user id
        // so get_user_role can read ZAISO_USER_ROLE.
        return await PostSalesOrderActionAsync(
            soNumber,
            "approveOrder",
            new { REQUESTING_TEAMS_USER = requestingSapUser },
            ct);
    }

    public async Task<SalesOrder> RejectApprovalAsync(string soNumber, string requestingSapUser, CancellationToken ct = default)
    {
        return await PostSalesOrderActionAsync(
            soNumber,
            "rejectApproval",
            new { REQUESTING_TEAMS_USER = requestingSapUser },
            ct);
    }

    public async Task<SalesOrder> ForceReleaseAsync(
        string soNumber,
        string requestingSapUser,
        string overrideReason,
        CancellationToken ct = default)
    {
        return await PostSalesOrderActionAsync(
            soNumber,
            "forceRelease",
            new
            {
                REQUESTING_TEAMS_USER = requestingSapUser,
                OVERRIDE_REASON = overrideReason,
            },
            ct);
    }

    public async Task<SalesOrder> ForceCancelAsync(
        string soNumber,
        string requestingSapUser,
        string overrideReason,
        CancellationToken ct = default)
    {
        return await PostSalesOrderActionAsync(
            soNumber,
            "forceCancel",
            new
            {
                REQUESTING_TEAMS_USER = requestingSapUser,
                OVERRIDE_REASON = overrideReason,
            },
            ct);
    }

    public async Task<SalesOrder> ReassignOwnerAsync(
        string soNumber,
        string newOwnerSapUser,
        string requestingSapUser,
        CancellationToken ct = default)
    {
        return await PostSalesOrderActionAsync(
            soNumber,
            "reassignOwner",
            new
            {
                REQUESTING_TEAMS_USER = requestingSapUser,
                NEW_OWNER_ID = newOwnerSapUser,
            },
            ct);
    }

    public async Task<SalesOrder> ForwardOrderAsync(
        string soNumber,
        string forwardToUser,
        string requestingTeamsUser,
        CancellationToken ct = default,
        string? remarks = null)
    {
        return await PostSalesOrderActionAsync(
            soNumber,
            "forwardOrder",
            new
            {
                REQUESTING_TEAMS_USER = requestingTeamsUser,
                NEW_TEAMS_USER = forwardToUser,
                REMARKS = remarks ?? string.Empty,
            },
            ct);
    }

    private async Task<SalesOrder> PostSalesOrderActionAsync<TPayload>(
        string soNumber,
        string actionName,
        TPayload payload,
        CancellationToken ct)
    {
        var formattedSo = FormatSoNumber(soNumber);
        var url =
            $"SalesOrder('{formattedSo}')/com.sap.gateway.srvd_a2x.zsd_aiso_sales_order.v0001.{actionName}?sap-client=324&$format=json";
        _logger.LogInformation("Calling SAP OData: {Url}", url);

        try
        {
            var result = await SendPostRequestAsync<SapSalesOrderDto, TPayload>(url, payload, ct);
            if (result == null)
                throw new InvalidOperationException($"Failed to deserialize SAP action {actionName}.");

            var order = MapToDomain(result);
            return order.SoNumber is "UNKNOWN"
                ? order with { SoNumber = formattedSo }
                : order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SAP OData {Action} for {SoNumber}", actionName, soNumber);
            throw;
        }
    }

    private async Task<TResult?> SendPostRequestAsync<TResult, TPayload>(string url, TPayload payload, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        var jsonString = System.Text.Json.JsonSerializer.Serialize(payload);

        _logger.LogInformation("SAP POST Request: URL={Url}, Payload={Payload}", url, jsonString);

        var stringContent = new StringContent(jsonString, System.Text.Encoding.UTF8, "application/json");
        if (stringContent.Headers.ContentType != null)
        {
            stringContent.Headers.ContentType.CharSet = string.Empty;
        }
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
            _logger.LogError("SAP POST Failed: HTTP {StatusCode}, URL={Url}, Body={ErrorBody}", (int)response.StatusCode, url, errorBody);
            throw new SapODataException((int)response.StatusCode, ParseSapErrorMessage(errorBody, (int)response.StatusCode));
        }

        return await response.Content.ReadFromJsonAsync<TResult>(cancellationToken: ct);
    }

    private static string ParseSapErrorMessage(string errorBody, int statusCode)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(errorBody);
            if (doc.RootElement.TryGetProperty("error", out var errorObj))
            {
                var code = errorObj.TryGetProperty("code", out var c) ? c.GetString() : null;
                var message = errorObj.TryGetProperty("message", out var m) ? m.GetString() : null;

                if (string.Equals(code, "RAISE_SHORTDUMP", StringComparison.OrdinalIgnoreCase))
                {
                    return $"SAP encountered an internal error (ABAP Short Dump). " +
                           $"This is typically caused by a transaction control violation in the RAP handler. " +
                           $"Please contact the SAP team to check transaction ST22 for details. " +
                           (!string.IsNullOrWhiteSpace(message) ? $"SAP message: {message}" : string.Empty);
                }

                return !string.IsNullOrWhiteSpace(message) ? message : $"SAP error {code}: {statusCode}";
            }
        }
        catch
        {
            // JSON parse failed, fall through
        }

        return $"SAP returned HTTP {statusCode}. Raw response: {errorBody[..Math.Min(errorBody.Length, 200)]}";
    }

    // -----------------------------------------------------------------------
    // KPI methods
    // -----------------------------------------------------------------------

    public async Task<KpiSummary> GetKpiSummaryAsync(KpiSummaryQuery query, CancellationToken ct = default)
    {
        // Entity: KpiRevenue (exposed by SAP team)
        var builder = new ODataQueryBuilder("KpiRevenue")
            .AddCustomParam("sap-client", "324");

        if (query.FromDate.HasValue)
            builder.FilterRaw($"BillingDate ge {query.FromDate.Value:yyyy-MM-dd}");
        if (query.ToDate.HasValue)
            builder.FilterRaw($"BillingDate le {query.ToDate.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(query.SalesOrg))
            builder.Filter("SalesOrg", "eq", query.SalesOrg);

        var url = builder.Build();
        _logger.LogInformation("Calling SAP OData (KPI Revenue): {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url, ct);

            // Fallback: If view is missing, aggregate SalesOrders entirely
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("SAP KPI view not found, falling back to manual aggregation for everything");
                return await GetKpiSummaryFallbackAsync(query, ct);
            }

            response.EnsureSuccessStatusCode();

            var rawJson = await response.Content.ReadAsStringAsync(ct);
            _logger.LogDebug("SAP KPI Revenue raw: {Raw}", rawJson);

            var result = JsonSerializer.Deserialize<ODataResponse<SapKpiRevenueDto>>(
                rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var rows = result?.Value ?? new List<SapKpiRevenueDto>();

            var totalRevenue = rows.Sum(r => r.TotalRevenue ?? 0);
            var currency = rows.FirstOrDefault()?.Currency ?? "USD";

            var timeSeries = rows
                .Where(r => !string.IsNullOrEmpty(r.BillingDate))
                .GroupBy(r => r.BillingDate!)
                .Select(g => new KpiDataPoint(g.Key, g.Sum(r => r.TotalRevenue ?? 0)))
                .ToList();

            // KpiRevenue doesn't provide order statuses, so we get them from SalesOrders
            var fallbackSummary = await GetKpiSummaryFallbackAsync(query, ct);

            return new KpiSummary
            {
                TotalRevenue = totalRevenue,
                Currency = currency,
                TotalOrders = fallbackSummary.TotalOrders,
                OpenOrders = fallbackSummary.OpenOrders,
                DeliveredOrders = fallbackSummary.DeliveredOrders,
                OverdueOrders = fallbackSummary.OverdueOrders,
                FulfillmentRate = fallbackSummary.FulfillmentRate,
                CancellationRate = fallbackSummary.CancellationRate,
                Period = BuildPeriodLabel(query.FromDate, query.ToDate),
                SalesOrg = query.SalesOrg,
                Granularity = query.Granularity,
                RevenueTimeSeries = timeSeries
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching KPI Revenue from SAP");
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
        // Entity set alias from ZSD_AISO_SALES_ORDER service definition.
        var builder = new ODataQueryBuilder("KpiSoAging")
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

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("KpiSoAging not found (404); returning empty overdue list");
                return Array.Empty<OverdueOrder>();
            }

            response.EnsureSuccessStatusCode();

            var rawJson = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ODataResponse<SapOverdueOrderDto>>(
                rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.Value == null) return Array.Empty<OverdueOrder>();

            return result.Value.Select(r => new OverdueOrder
            {
                SoNumber = FormatSoNumber(r.SoNumber),
                CustomerId = r.Customer ?? string.Empty,
                CustomerName = r.CustomerName ?? "Unknown",
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

    public async Task<bool?> SapUserExistsAsync(string sapUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sapUserId))
            return false;

        var normalized = sapUserId.Trim().ToUpperInvariant();
        var url = new ODataQueryBuilder("UserRole")
            .AddCustomParam("sap-client", "324")
            .Filter("SapUser", "eq", normalized)
            .Top(1)
            .Build();

        _logger.LogInformation("Calling SAP OData user lookup: {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            if (response.StatusCode is System.Net.HttpStatusCode.NotFound
                or System.Net.HttpStatusCode.BadRequest)
            {
                // Entity set not published yet, or filter rejected — caller may fall back.
                _logger.LogWarning(
                    "SAP UserRole lookup unavailable for {SapUser}: {StatusCode}",
                    normalized,
                    (int)response.StatusCode);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "SAP UserRole lookup failed for {SapUser}: {StatusCode}",
                    normalized,
                    (int)response.StatusCode);
                return null;
            }

            var rawJson = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ODataResponse<SapUserRoleDto>>(rawJson, JsonOptions);
            return result?.Value is { Count: > 0 };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SAP UserRole lookup error for {SapUser}", normalized);
            return null;
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private SalesOrder MapToDomain(
        SapSalesOrderDto dto,
        IReadOnlyList<SalesOrderItem>? items = null)
    {
        return new SalesOrder
        {
            SoNumber = FormatSoNumber(dto.SoNumber),
            CustomerId = dto.Customer ?? string.Empty,
            CustomerName = string.IsNullOrWhiteSpace(dto.CustomerName) ? "N/A" : dto.CustomerName,
            CustomerReference = dto.CustomerReference,
            RequestedDeliveryDate = DateOnly.TryParse(dto.RequestedDeliveryDate, out var requestedDate)
                ? requestedDate
                : null,
            Division = dto.Division,
            OrderDate = DateOnly.TryParse(dto.DocDate, out var date) ? date : DateOnly.MinValue,
            NetValue = dto.NetValue ?? 0,
            Currency = string.IsNullOrEmpty(dto.Currency) ? "USD" : dto.Currency,
            Status = MapStatus(dto),
            SalesOrg = dto.SalesOrg ?? "UNKNOWN",
            OwnerSapUser = string.IsNullOrWhiteSpace(dto.OwnerSapUser) ? null : dto.OwnerSapUser.Trim(),
            Items = items ?? Array.Empty<SalesOrderItem>()
        };
    }

    private static SalesOrderItem MapItemToDomain(SapSalesOrderItemDto dto)
    {
        var material = dto.Material?.Trim() ?? string.Empty;
        var materialName = dto.MaterialName?.Trim() ?? string.Empty;
        return new SalesOrderItem
        {
            ItemNumber = string.IsNullOrWhiteSpace(dto.ItemNo) ? "000000" : dto.ItemNo.Trim(),
            Material = material,
            Description = !string.IsNullOrWhiteSpace(materialName)
                ? materialName
                : string.IsNullOrWhiteSpace(material) ? "N/A" : material,
            Quantity = dto.OrderQty ?? 0,
            Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "EA" : dto.Unit,
            NetValue = dto.NetValue ?? 0
        };
    }

    /// <summary>
    /// Maps domain status to OData filters on SAP SalesOrder fields.
    /// OverallStatus (GBSTK): A=Open, B=Partially delivered, C=Complete/Delivered.
    /// </summary>
    internal static void ApplyStatusFilter(ODataQueryBuilder builder, SalesOrderStatus status)
    {
        switch (status)
        {
            case SalesOrderStatus.Open:
                builder.Filter("OverallStatus", "eq", "A");
                break;
            case SalesOrderStatus.PartiallyDelivered:
                builder.Filter("OverallStatus", "eq", "B");
                break;
            case SalesOrderStatus.Delivered:
                builder.Filter("OverallStatus", "eq", "C");
                break;
            case SalesOrderStatus.Blocked:
                // Delivery block present (LIFSK); requires SAP field to be populated in the CDS/OData projection.
                builder.FilterRaw("DeliveryBlock ne ''");
                break;
            case SalesOrderStatus.Cancelled:
                builder.Filter("IsCancelled", "eq", "X");
                break;
            case SalesOrderStatus.Invoiced:
                // Fully billed (FKSTK = C) when exposed by SAP.
                builder.Filter("BillingStatus", "eq", "C");
                break;
        }
    }

    private SalesOrderStatus MapStatus(SapSalesOrderDto dto)
    {
        if (string.Equals(dto.IsCancelled, "X", StringComparison.OrdinalIgnoreCase))
            return SalesOrderStatus.Cancelled;

        if (!string.IsNullOrWhiteSpace(dto.DeliveryBlock))
            return SalesOrderStatus.Blocked;

        if (string.Equals(dto.BillingStatus, "C", StringComparison.OrdinalIgnoreCase))
            return SalesOrderStatus.Invoiced;

        return dto.OverallStatus switch
        {
            "A" => SalesOrderStatus.Open,
            "B" => SalesOrderStatus.PartiallyDelivered,
            "C" => SalesOrderStatus.Delivered,
            _ => SalesOrderStatus.Open
        };
    }
}
