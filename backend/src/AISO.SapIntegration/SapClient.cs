using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AISO.Domain.Kpi;
using AISO.Domain.SalesOrders;
using AISO.Domain.Approvals;
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

    /// <summary>KUNNR / partner number — numeric IDs are alpha-converted to 10 digits.</summary>
    internal static string FormatCustomerNumber(string? customer)
    {
        if (string.IsNullOrWhiteSpace(customer))
            return string.Empty;

        var raw = customer.Trim();
        if (raw.All(char.IsDigit))
            return raw.PadLeft(10, '0');

        return raw.ToUpperInvariant();
    }

    public async Task<IReadOnlyList<SalesOrder>> GetSalesOrdersAsync(SalesOrdersQuery query, CancellationToken ct = default)
    {
        var builder = new ODataQueryBuilder("SalesOrder")
            .AddCustomParam("sap-client", "324")
            .Top(query.Top);

        if (!string.IsNullOrWhiteSpace(query.CustomerIdOrName))
        {
            ApplyCustomerIdOrNameFilter(builder, query.CustomerIdOrName);
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

        if (!string.IsNullOrWhiteSpace(query.OwnerSapUser))
        {
            builder.Filter("OwnerSapUser", "eq", query.OwnerSapUser.Trim());
        }

        // Default: hide SOs with missing material master (MARA) from list/KPI pickers.
        // Empty-string filter must use FilterRaw — Filter() skips blank values.
        if (query.ExcludeInvalidMaterials)
        {
            builder.FilterRaw("HasInvalidMaterial eq ''");
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
            var (items, allItemsRejected) = await GetSalesOrderItemsAsync(formattedSo, ct);
            return MapToDomain(dto, items, allItemsRejected);
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
    private async Task<(IReadOnlyList<SalesOrderItem> Items, bool AllItemsRejected)> GetSalesOrderItemsAsync(
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
                return (Array.Empty<SalesOrderItem>(), false);
            }

            var rawJson = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ODataResponse<SapSalesOrderItemDto>>(rawJson, JsonOptions);
            if (result?.Value is null || result.Value.Count == 0)
                return (Array.Empty<SalesOrderItem>(), false);

            var allRejected = result.Value.All(i => !string.IsNullOrWhiteSpace(i.RejectionRsn));
            var items = result.Value.Select(MapItemToDomain).ToList();
            return (items, allRejected);
        }
        catch (Exception ex)
        {
            // Detail card should still render header if items temporarily fail.
            _logger.LogWarning(ex, "Failed to load SalesOrderItem for {SoNumber}", formattedSoNumber);
            return (Array.Empty<SalesOrderItem>(), false);
        }
    }

    public async Task<SalesOrder> CreateSalesOrderAsync(CreateSalesOrderDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.RequestingSapUser))
            throw new ArgumentException("RequestingSapUser is required for createSalesOrder.", nameof(dto));

        var url = "SalesOrder/com.sap.gateway.srvd_a2x.zsd_aiso_sales_order.v0001.createSalesOrder?sap-client=324&$format=json";
        _logger.LogInformation("Calling SAP OData: {Url}", url);

        var payload = new
        {
            DOC_TYPE = dto.DocType,
            SALES_ORG = dto.SalesOrg,
            DIST_CHANNEL = dto.DistChannel,
            DIVISION = dto.Division,
            CUSTOMER = FormatCustomerNumber(dto.Customer),
            CURRENCY = dto.Currency,
            REQUESTING_TEAMS_USER = dto.RequestingSapUser.Trim(),
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
            using var response = await SendPostRequestRawAsync(url, payload, ct);
            var rawJson = await response.Content.ReadAsStringAsync(ct);
            var soNumber = TryExtractSoNumber(rawJson);

            if (string.IsNullOrWhiteSpace(soNumber))
            {
                _logger.LogWarning("createSalesOrder response had no SoNumber. Body={Body}", rawJson);
                throw new InvalidOperationException("SAP createSalesOrder succeeded but returned no sales order number.");
            }

            var formatted = FormatSoNumber(soNumber);
            var refreshed = await GetSalesOrderByIdAsync(formatted, ct);
            if (refreshed is not null)
                return refreshed;

            return new SalesOrder
            {
                SoNumber = formatted,
                CustomerId = dto.Customer,
                CustomerName = string.Empty,
                SalesOrg = dto.SalesOrg,
                OrderDate = DateOnly.FromDateTime(DateTime.UtcNow),
                NetValue = 0,
                Currency = dto.Currency,
                Status = SalesOrderStatus.Open,
                OwnerSapUser = dto.RequestingSapUser.Trim(),
                Items = Array.Empty<SalesOrderItem>()
            };
        }
        catch (Exception ex) when (ex is not SapODataException and not ArgumentException and not InvalidOperationException)
        {
            _logger.LogError(ex, "Error calling SAP OData CreateSalesOrderAsync");
            throw;
        }
    }

    public async Task SyncUserRoleAsync(
        string targetSapUser,
        string newRole,
        string? salesOrg,
        string requestingAdminSapUser,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetSapUser))
            throw new ArgumentException("Target SAP user is required.", nameof(targetSapUser));
        if (string.IsNullOrWhiteSpace(newRole))
            throw new ArgumentException("New role is required.", nameof(newRole));
        if (string.IsNullOrWhiteSpace(requestingAdminSapUser))
            throw new ArgumentException("Requesting admin SAP user is required.", nameof(requestingAdminSapUser));

        var url = "UserRole/com.sap.gateway.srvd_a2x.zsd_aiso_sales_order.v0001.syncUserRole?sap-client=324&$format=json";
        _logger.LogInformation("Calling SAP OData: {Url}", url);

        var payload = new
        {
            SAP_USER = targetSapUser.Trim().ToUpperInvariant(),
            NEW_ROLE = newRole.Trim().ToUpperInvariant(),
            SALES_ORG = string.IsNullOrWhiteSpace(salesOrg) ? string.Empty : salesOrg.Trim().ToUpperInvariant(),
            REQUESTING_TEAMS_USER = requestingAdminSapUser.Trim()
        };

        try
        {
            using var _ = await SendPostRequestRawAsync(url, payload, ct);
        }
        catch (Exception ex) when (ex is not SapODataException and not ArgumentException)
        {
            _logger.LogError(ex, "Error calling SAP OData SyncUserRoleAsync for {SapUser}", targetSapUser);
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

    public async Task<SalesOrder> UpdateSalesOrderAsync(UpdateSalesOrderDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.RequestingSapUser))
            throw new ArgumentException("RequestingSapUser is required for updateSalesOrder.", nameof(dto));

        var formattedSo = FormatSoNumber(dto.SoNumber);
        var url = $"SalesOrder('{formattedSo}')/com.sap.gateway.srvd_a2x.zsd_aiso_sales_order.v0001.updateSalesOrder?sap-client=324&$format=json";
        _logger.LogInformation("Calling SAP OData: {Url}", url);

        // Align with ZAISO_A_UPDATE_SO: NEW_REFERENCE, REQUESTED_DELIVERY_DATE, ITEMS
        // (no CHANGE_* flags — SAP updates a header field only when it is non-initial).
        var payload = new
        {
            REQUESTING_TEAMS_USER = dto.RequestingSapUser.Trim(),
            NEW_REFERENCE = string.IsNullOrWhiteSpace(dto.PurchaseOrderRef)
                ? string.Empty
                : dto.PurchaseOrderRef.Trim(),
            REQUESTED_DELIVERY_DATE = string.IsNullOrWhiteSpace(dto.ReqDeliveryDate)
                ? null
                : NormalizeSapDate(dto.ReqDeliveryDate),
            ITEMS = (dto.Items ?? Array.Empty<UpdateSalesOrderItemDto>()).Select(i => new
            {
                ITEM_NO = PadItemNumber(i.ItemNumber),
                MATERIAL = i.Material ?? string.Empty,
                ORDER_QTY = i.OrderQty ?? 0m,
                UNIT = i.Unit ?? string.Empty,
                CHANGE_FLAG = (i.Operation ?? string.Empty).Trim().ToUpperInvariant()
            }).ToList()
        };

        try
        {
            var result = await SendPostRequestAsync<SapSalesOrderDto, object>(url, payload, ct);
            if (result == null)
                throw new InvalidOperationException("Failed to deserialize updated order.");

            var refreshed = await GetSalesOrderByIdAsync(formattedSo, ct);
            return refreshed ?? MapToDomain(result);
        }
        catch (Exception ex) when (ex is not SapODataException and not ArgumentException and not InvalidOperationException)
        {
            _logger.LogError(ex, "Error calling SAP OData UpdateSalesOrderAsync for {SoNumber}", dto.SoNumber);
            throw;
        }
    }

    private static string PadItemNumber(string? itemNumber)
    {
        if (string.IsNullOrWhiteSpace(itemNumber))
            return "000000";
        var digits = new string(itemNumber.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digits))
            return "000000";
        return digits.PadLeft(6, '0');
    }

    private static string? NormalizeSapDate(string value)
    {
        var trimmed = value.Trim();
        if (DateOnly.TryParse(trimmed, out var d))
            return d.ToString("yyyy-MM-dd");
        if (trimmed.Length == 8 && trimmed.All(char.IsDigit))
            return $"{trimmed[..4]}-{trimmed[4..6]}-{trimmed[6..8]}";
        return trimmed;
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

            // RAP action returns only %tky — re-GET so IsCancelled / item RejectionRsn are visible.
            var refreshed = await GetSalesOrderByIdAsync(formattedSo, ct);
            if (refreshed is not null)
                return refreshed;

            var order = MapToDomain(result);
            // Reject succeeded; if GET is unavailable, still treat as Cancelled for UX.
            return order with
            {
                SoNumber = order.SoNumber is "UNKNOWN" ? formattedSo : order.SoNumber,
                Status = SalesOrderStatus.Cancelled
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SAP OData RejectOrderAsync for {SoNumber}", soNumber);
            throw;
        }
    }

    public async Task<SalesOrder> CancelOrderAsync(
        string soNumber,
        string requestingSapUser,
        string? reason = null,
        CancellationToken ct = default)
    {
        var formattedSo = FormatSoNumber(soNumber);
        var url = $"SalesOrder('{formattedSo}')/com.sap.gateway.srvd_a2x.zsd_aiso_sales_order.v0001.cancelOrder?sap-client=324&$format=json";
        _logger.LogInformation("Calling SAP OData: {Url}", url);

        var payload = new
        {
            REQUESTING_TEAMS_USER = requestingSapUser,
            REASON = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason.Trim(),
        };

        try
        {
            var result = await SendPostRequestAsync<SapSalesOrderDto, object>(url, payload, ct);
            if (result == null)
                throw new InvalidOperationException("Failed to deserialize cancelled order.");

            var refreshed = await GetSalesOrderByIdAsync(formattedSo, ct);
            if (refreshed is not null)
                return refreshed;

            var order = MapToDomain(result);
            return order with
            {
                SoNumber = order.SoNumber is "UNKNOWN" ? formattedSo : order.SoNumber,
                Status = SalesOrderStatus.Cancelled
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SAP OData CancelOrderAsync for {SoNumber}", soNumber);
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

            // Action responses are often sparse (NetValue/items missing). Re-read full SO.
            // Mock/tests may return the same sparse body for GET — always keep the requested SoNumber.
            var refreshed = await GetSalesOrderByIdAsync(formattedSo, ct);
            if (refreshed is not null)
            {
                return refreshed.SoNumber is "UNKNOWN" || string.IsNullOrWhiteSpace(refreshed.SoNumber)
                    ? refreshed with { SoNumber = formattedSo }
                    : refreshed;
            }

            var order = MapToDomain(result);
            return order.SoNumber is "UNKNOWN" || string.IsNullOrWhiteSpace(order.SoNumber)
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
        using var response = await SendPostRequestRawAsync(url, payload, ct);
        return await response.Content.ReadFromJsonAsync<TResult>(cancellationToken: ct);
    }

    private async Task<HttpResponseMessage> SendPostRequestRawAsync<TPayload>(string url, TPayload payload, CancellationToken ct)
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
            var statusCode = (int)response.StatusCode;
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("SAP POST Failed: HTTP {StatusCode}, URL={Url}, Body={ErrorBody}", statusCode, url, errorBody);
            response.Dispose();
            throw new SapODataException(statusCode, ParseSapErrorMessage(errorBody, statusCode));
        }

        return response;
    }

    private static string? TryExtractSoNumber(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(rawJson);
            return FindSoNumber(doc.RootElement);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string? FindSoNumber(System.Text.Json.JsonElement element)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var name in new[] { "SoNumber", "SO_NUMBER", "SalesOrder", "salesOrder", "salesdocument" })
            {
                if (element.TryGetProperty(name, out var prop)
                    && prop.ValueKind == System.Text.Json.JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(prop.GetString()))
                {
                    return prop.GetString();
                }
            }

            if (element.TryGetProperty("value", out var value))
            {
                var nested = FindSoNumber(value);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }

            foreach (var property in element.EnumerateObject())
            {
                var nested = FindSoNumber(property.Value);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }
        }
        else if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindSoNumber(item);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }
        }

        return null;
    }

    private static string ParseSapErrorMessage(string errorBody, int statusCode)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(errorBody);
            if (!doc.RootElement.TryGetProperty("error", out var errorObj))
            {
                return BuildFallbackMessage(null, statusCode);
            }

            var candidates = new List<string>();

            CollectAllMessages(errorObj, candidates);

            var code = errorObj.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString()
                : null;

            // Prefer the most specific (longest) business message from details,
            // but filter out single-char placeholders like "M" (SAP message class).
            var message = candidates
                .Where(m => !string.IsNullOrWhiteSpace(m) && m.Length > 1)
                .OrderByDescending(m => m.Length)
                .FirstOrDefault();

            if (string.Equals(code, "RAISE_SHORTDUMP", StringComparison.OrdinalIgnoreCase))
            {
                return $"SAP encountered an internal error (ABAP Short Dump). " +
                       $"This is typically caused by a transaction control violation in the RAP handler. " +
                       $"Please contact the SAP team to check transaction ST22 for details. " +
                       (!string.IsNullOrWhiteSpace(message) ? $"SAP message: {message}" : string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(message))
                return message!;

            return BuildFallbackMessage(code, statusCode);
        }
        catch
        {
            // JSON parse failed, fall through
        }

        return BuildFallbackMessage(null, statusCode);
    }

    /// <summary>
    /// Builds a user-friendly fallback message without exposing raw JSON.
    /// </summary>
    private static string BuildFallbackMessage(string? sapCode, int statusCode)
    {
        var codeDisplay = !string.IsNullOrWhiteSpace(sapCode) ? sapCode : "UNKNOWN";
        return statusCode switch
        {
            400 => $"SAP rejected the request (error code: {codeDisplay}). Please verify the input data and try again.",
            401 or 403 => $"SAP authorization failed (error code: {codeDisplay}). Please check your permissions or contact your admin.",
            404 => $"The requested SAP resource was not found (error code: {codeDisplay}). Please verify the endpoint configuration or contact your admin.",
            409 => $"SAP reported a conflict (error code: {codeDisplay}). The record may have been modified by another user. Please try again.",
            500 => $"SAP encountered an internal server error (error code: {codeDisplay}). Please try again later or contact the SAP team.",
            502 or 503 or 504 => $"SAP service is temporarily unavailable (HTTP {statusCode}, error code: {codeDisplay}). Please try again in a few minutes.",
            _ => $"SAP could not complete this request (HTTP {statusCode}, error code: {codeDisplay}). Please try again or contact your admin."
        };
    }

    private static void CollectAllMessages(JsonElement element, List<string> sink)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("message", out var msgEl))
            {
                if (msgEl.ValueKind == JsonValueKind.String)
                {
                    var s = msgEl.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        sink.Add(s.Trim());
                }
                else if (msgEl.ValueKind == JsonValueKind.Object
                         && msgEl.TryGetProperty("value", out var valEl)
                         && valEl.ValueKind == JsonValueKind.String)
                {
                    var s = valEl.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        sink.Add(s.Trim());
                }
            }

            foreach (var prop in element.EnumerateObject())
            {
                CollectAllMessages(prop.Value, sink);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectAllMessages(item, sink);
            }
        }
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
        // Preferred: dedicated OData entity when SAP publishes it (alias KpiByCustomer).
        // Fallback: aggregate from SalesOrder — current service has no by-customer KPI view.
        var builder = new ODataQueryBuilder("KpiByCustomer")
            .AddCustomParam("sap-client", "324")
            .Top(query.Top);

        if (!string.IsNullOrWhiteSpace(query.CustomerIdOrName))
            ApplyCustomerIdOrNameFilter(builder, query.CustomerIdOrName);
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
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("KpiByCustomer not found (404); aggregating from SalesOrder");
                return await GetKpiByCustomerFallbackAsync(query, ct);
            }

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
            _logger.LogError(ex, "Error calling SAP KPI By Customer endpoint; trying SalesOrder fallback");
            return await GetKpiByCustomerFallbackAsync(query, ct);
        }
    }

    private async Task<IReadOnlyList<KpiByCustomer>> GetKpiByCustomerFallbackAsync(
        KpiByCustomerQuery query,
        CancellationToken ct)
    {
        var orders = await GetSalesOrdersAsync(new SalesOrdersQuery
        {
            CustomerIdOrName = query.CustomerIdOrName,
            FromDate = query.FromDate,
            ToDate = query.ToDate,
            SalesOrg = query.SalesOrg,
            Top = 500
        }, ct);

        return orders
            .GroupBy(o => new
            {
                CustomerId = o.CustomerId ?? string.Empty,
                CustomerName = string.IsNullOrWhiteSpace(o.CustomerName) ? (o.CustomerId ?? string.Empty) : o.CustomerName
            })
            .Select(g => new KpiByCustomer
            {
                CustomerId = g.Key.CustomerId,
                CustomerName = g.Key.CustomerName,
                Revenue = g.Sum(o => o.NetValue),
                Currency = g.FirstOrDefault()?.Currency ?? "USD",
                OrderCount = g.Count(),
                FulfillmentRate = g.Count() > 0
                    ? Math.Round(g.Count(o => o.Status == SalesOrderStatus.Delivered) * 100m / g.Count(), 1)
                    : 0
            })
            .OrderByDescending(c => c.Revenue)
            .Take(query.Top)
            .ToList();
    }

    public async Task<IReadOnlyList<KpiByProduct>> GetKpiByProductAsync(KpiByProductQuery query, CancellationToken ct = default)
    {
        // Preferred: dedicated OData entity when SAP publishes it (alias KpiByProduct).
        // Fallback: aggregate from SalesOrderItem.
        var builder = new ODataQueryBuilder("KpiByProduct")
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
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("KpiByProduct not found (404); aggregating from SalesOrderItem");
                return await GetKpiByProductFallbackAsync(query, ct);
            }

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
            _logger.LogError(ex, "Error calling SAP KPI By Product endpoint; trying SalesOrderItem fallback");
            return await GetKpiByProductFallbackAsync(query, ct);
        }
    }

    private async Task<IReadOnlyList<KpiByProduct>> GetKpiByProductFallbackAsync(
        KpiByProductQuery query,
        CancellationToken ct)
    {
        var itemBuilder = new ODataQueryBuilder("SalesOrderItem")
            .AddCustomParam("sap-client", "324")
            .Top(500);

        if (!string.IsNullOrWhiteSpace(query.MaterialIdOrName))
            itemBuilder.Filter("Material", "eq", query.MaterialIdOrName);

        var url = itemBuilder.Build();
        _logger.LogInformation("Calling SAP OData (SalesOrderItem for product KPI): {Url}", url);

        var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "SalesOrderItem product KPI fallback failed: {StatusCode}",
                (int)response.StatusCode);
            return Array.Empty<KpiByProduct>();
        }

        var rawJson = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<ODataResponse<SapSalesOrderItemDto>>(rawJson, JsonOptions);
        var items = result?.Value ?? new List<SapSalesOrderItemDto>();

        if (!string.IsNullOrWhiteSpace(query.MaterialIdOrName))
        {
            var needle = query.MaterialIdOrName.Trim();
            items = items
                .Where(i =>
                    string.Equals(i.Material, needle, StringComparison.OrdinalIgnoreCase)
                    || (i.MaterialName?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        return items
            .GroupBy(i => new
            {
                MaterialId = i.Material ?? string.Empty,
                MaterialName = string.IsNullOrWhiteSpace(i.MaterialName) ? (i.Material ?? string.Empty) : i.MaterialName!
            })
            .Select(g => new KpiByProduct
            {
                MaterialId = g.Key.MaterialId,
                MaterialName = g.Key.MaterialName,
                Revenue = g.Sum(i => i.NetValue ?? 0),
                Currency = g.FirstOrDefault()?.Currency ?? "USD",
                TotalQty = g.Sum(i => i.OrderQty ?? 0),
                Unit = g.FirstOrDefault()?.Unit ?? "PC",
                OrderCount = g.Select(i => i.SoNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            })
            .OrderByDescending(p => p.Revenue)
            .Take(query.Top)
            .ToList();
    }

    public async Task<IReadOnlyList<OverdueOrder>> GetOverdueOrdersAsync(OverdueOrdersQuery query, CancellationToken ct = default)
    {
        // Entity set alias from ZSD_AISO_SALES_ORDER service definition.
        var builder = new ODataQueryBuilder("KpiSoAging")
            .AddCustomParam("sap-client", "324")
            .Top(query.Top);

        if (!string.IsNullOrWhiteSpace(query.CustomerIdOrName))
            ApplyCustomerIdOrNameFilter(builder, query.CustomerIdOrName);
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

            return result.Value
                .Select(r => new OverdueOrder
                {
                    SoNumber = FormatSoNumber(r.SoNumber),
                    CustomerId = r.Customer ?? string.Empty,
                    CustomerName = r.CustomerName ?? "Unknown",
                    ScheduledDeliveryDate = DateOnly.TryParse(r.ScheduledDeliveryDate, out var d) ? d : DateOnly.MinValue,
                    DaysPastDue = r.DaysPastDue ?? 0,
                    NetValue = r.NetValue ?? 0,
                    Currency = r.Currency ?? "USD",
                    SalesOrg = r.SalesOrg ?? string.Empty
                })
                .OrderByDescending(o => o.DaysPastDue)
                .Take(query.Top)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling SAP Overdue Orders endpoint");
            throw;
        }
    }

    public async Task<IReadOnlyList<SapSalesArea>> GetSalesAreasAsync(CancellationToken ct = default)
    {
        var url = new ODataQueryBuilder("SalesArea")
            .AddCustomParam("sap-client", "324")
            .Top(200)
            .Build();

        _logger.LogInformation("Calling SAP OData: {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            if (response.StatusCode is System.Net.HttpStatusCode.NotFound
                or System.Net.HttpStatusCode.BadRequest)
            {
                _logger.LogWarning("SalesArea entity unavailable: {StatusCode}", (int)response.StatusCode);
                return Array.Empty<SapSalesArea>();
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SalesArea GET failed: {StatusCode}", (int)response.StatusCode);
                return Array.Empty<SapSalesArea>();
            }

            var rawJson = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ODataResponse<SapSalesAreaDto>>(rawJson, JsonOptions);
            if (result?.Value is null || result.Value.Count == 0)
                return Array.Empty<SapSalesArea>();

            return result.Value
                .Where(r => !string.IsNullOrWhiteSpace(r.SalesOrg)
                            && !string.IsNullOrWhiteSpace(r.DistChannel)
                            && !string.IsNullOrWhiteSpace(r.Division))
                .Select(r => new SapSalesArea(
                    r.SalesOrg!.Trim().ToUpperInvariant(),
                    r.DistChannel!.Trim().ToUpperInvariant(),
                    r.Division!.Trim().ToUpperInvariant()))
                .GroupBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(a => a.SalesOrg, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.DistChannel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.Division, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is not SapODataException)
        {
            _logger.LogWarning(ex, "SalesArea lookup failed");
            return Array.Empty<SapSalesArea>();
        }
    }

    public async Task<IReadOnlyList<SapValidCustomer>> GetValidCustomersAsync(
        string? salesOrg = null,
        string? distChannel = null,
        string? division = null,
        int top = 100,
        CancellationToken ct = default)
    {
        return await GetValidCustomersAsync(
            customer: null,
            salesOrg,
            distChannel,
            division,
            top,
            ct);
    }

    private async Task<IReadOnlyList<SapValidCustomer>> GetValidCustomersAsync(
        string? customer,
        string? salesOrg,
        string? distChannel,
        string? division,
        int top,
        CancellationToken ct)
    {
        var take = Math.Clamp(top, 1, 200);
        var builder = new ODataQueryBuilder("ValidCustomer")
            .AddCustomParam("sap-client", "324")
            .Top(take);

        if (!string.IsNullOrWhiteSpace(customer))
        {
            // KUNNR is often alpha-padded; try exact value from the card first.
            builder.Filter("Customer", "eq", customer.Trim());
        }
        if (!string.IsNullOrWhiteSpace(salesOrg))
            builder.Filter("SalesOrg", "eq", salesOrg.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(distChannel))
            builder.Filter("DistChannel", "eq", distChannel.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(division))
            builder.Filter("Division", "eq", division.Trim().ToUpperInvariant());

        var url = builder.Build();
        _logger.LogInformation("Calling SAP OData: {Url}", url);

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            if (response.StatusCode is System.Net.HttpStatusCode.NotFound
                or System.Net.HttpStatusCode.BadRequest)
            {
                _logger.LogWarning("ValidCustomer entity unavailable: {StatusCode}", (int)response.StatusCode);
                return Array.Empty<SapValidCustomer>();
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ValidCustomer GET failed: {StatusCode}", (int)response.StatusCode);
                return Array.Empty<SapValidCustomer>();
            }

            var rawJson = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ODataResponse<SapValidCustomerDto>>(rawJson, JsonOptions);
            if (result?.Value is null || result.Value.Count == 0)
                return Array.Empty<SapValidCustomer>();

            return result.Value
                .Where(r => !string.IsNullOrWhiteSpace(r.Customer)
                            && !string.IsNullOrWhiteSpace(r.SalesOrg))
                .Select(r => new SapValidCustomer(
                    r.Customer!.Trim(),
                    r.SalesOrg!.Trim().ToUpperInvariant(),
                    (r.DistChannel ?? string.Empty).Trim().ToUpperInvariant(),
                    (r.Division ?? string.Empty).Trim().ToUpperInvariant(),
                    r.CustomerName))
                .ToList();
        }
        catch (Exception ex) when (ex is not SapODataException)
        {
            _logger.LogWarning(ex, "ValidCustomer lookup failed");
            return Array.Empty<SapValidCustomer>();
        }
    }

    public async Task<bool?> IsCustomerValidForSalesAreaAsync(
        string customer,
        string salesOrg,
        string distChannel,
        string division,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customer)
            || string.IsNullOrWhiteSpace(salesOrg)
            || string.IsNullOrWhiteSpace(distChannel)
            || string.IsNullOrWhiteSpace(division))
            return false;

        var raw = customer.Trim();
        var stripped = raw.TrimStart('0');
        if (string.IsNullOrEmpty(stripped))
            stripped = raw;

        // Prefer exact Customer filter (avoids false negatives from $top on sales-area lists).
        foreach (var candidate in new[] { raw, stripped, raw.PadLeft(10, '0') }.Distinct(StringComparer.Ordinal))
        {
            var rows = await GetValidCustomersAsync(
                customer: candidate,
                salesOrg: salesOrg.Trim(),
                distChannel: distChannel.Trim(),
                division: division.Trim(),
                top: 5,
                ct);

            if (rows.Count > 0)
                return true;
        }

        // Entity down vs real miss: probe any ValidCustomer row.
        var any = await GetValidCustomersAsync(top: 1, ct: ct);
        if (any.Count == 0)
            return null;

        return false;
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
        IReadOnlyList<SalesOrderItem>? items = null,
        bool allItemsRejected = false)
    {
        var mappedItems = items ?? Array.Empty<SalesOrderItem>();
        var headerNet = dto.NetValue ?? 0;
        // Some SAP action/GET payloads omit header NetValue after release; prefer item sum.
        var netValue = headerNet > 0
            ? headerNet
            : mappedItems.Sum(i => i.NetValue);

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
            NetValue = netValue,
            Currency = string.IsNullOrEmpty(dto.Currency) ? "USD" : dto.Currency,
            Status = MapStatus(dto, allItemsRejected),
            SalesOrg = dto.SalesOrg ?? "UNKNOWN",
            OwnerSapUser = string.IsNullOrWhiteSpace(dto.OwnerSapUser) ? null : dto.OwnerSapUser.Trim(),
            HasInvalidMaterial = IsSapFlagSet(dto.HasInvalidMaterial),
            Items = mappedItems
        };
    }

    private static bool IsSapFlagSet(string? flag) =>
        string.Equals(flag?.Trim(), "X", StringComparison.OrdinalIgnoreCase);

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
    /// Customer ID → <c>Customer eq</c>; customer name / partial name →
    /// <c>contains(CustomerName,'…')</c> (OData V4 / CAP).
    /// </summary>
    internal static void ApplyCustomerIdOrNameFilter(ODataQueryBuilder builder, string customerIdOrName)
    {
        var needle = customerIdOrName.Trim();
        if (needle.Length == 0)
            return;

        if (LooksLikeCustomerId(needle))
        {
            builder.Filter("Customer", "eq", needle);
            return;
        }

        var escaped = EscapeODataStringLiteral(needle);
        builder.FilterRaw($"contains(CustomerName,'{escaped}')");
    }

    /// <summary>
    /// Numeric KUNNR-style IDs and codes (USCU_*, CUST-*, alphanumeric with a digit)
    /// without whitespace are treated as IDs; everything else is a name search.
    /// </summary>
    internal static bool LooksLikeCustomerId(string value)
    {
        var s = value.Trim();
        if (s.Length == 0)
            return false;
        if (s.Any(char.IsWhiteSpace))
            return false;
        if (s.All(char.IsDigit))
            return true;
        if (s.StartsWith("USCU_", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("CUST-", StringComparison.OrdinalIgnoreCase))
            return true;
        return s.Any(char.IsDigit)
               && s.All(c => char.IsLetterOrDigit(c) || c is '_' or '-');
    }

    internal static string EscapeODataStringLiteral(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);

    /// <summary>
    /// Maps domain status to OData filters on SAP SalesOrder fields.
    /// OverallStatus (GBSTK): A=Open, B=Partially delivered, C=Complete/Delivered.
    /// </summary>
    public async Task DelegateApprovalAsync(DelegateApprovalDto dto, CancellationToken ct = default)
    {
        var url = "UserRole/com.sap.gateway.srvd_a2x.zsd_aiso_sales_order.v0001.delegateApproval?sap-client=324&$format=json";
        _logger.LogInformation("Calling SAP OData (delegateApproval): {Url}", url);

        var payload = new
        {
            REQUESTING_TEAMS_USER = dto.RequestingTeamsUser,
            DELEGATE_USER = dto.DelegateUser,
            SALES_ORG = dto.SalesOrg ?? string.Empty,
            VALID_FROM = dto.ValidFrom.ToString("yyyy-MM-dd"),
            VALID_TO = dto.ValidTo.ToString("yyyy-MM-dd"),
            REASON = dto.Reason ?? string.Empty
        };

        using var response = await SendPostRequestRawAsync(url, payload, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        _logger.LogDebug("SAP delegateApproval response: {Response}", json);
    }

    public async Task RevokeDelegationAsync(RevokeDelegationDto dto, CancellationToken ct = default)
    {
        var url = "UserRole/com.sap.gateway.srvd_a2x.zsd_aiso_sales_order.v0001.revokeDelegation?sap-client=324&$format=json";
        _logger.LogInformation("Calling SAP OData (revokeDelegation): {Url}", url);

        var payload = new
        {
            REQUESTING_TEAMS_USER = dto.RequestingTeamsUser,
            DELEGATION_ID = dto.DelegationId
        };

        using var response = await SendPostRequestRawAsync(url, payload, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        _logger.LogDebug("SAP revokeDelegation response: {Response}", json);
    }

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

    private static SalesOrderStatus MapStatus(SapSalesOrderDto dto, bool allItemsRejected = false)
    {
        // Prefer header IsCancelled (CDS ZI_AISO_SO_REJECT_STATUS); fall back to all
        // line items having RejectionRsn/ABGRU when the flag is missing or delayed.
        if (string.Equals(dto.IsCancelled, "X", StringComparison.OrdinalIgnoreCase)
            || allItemsRejected)
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
