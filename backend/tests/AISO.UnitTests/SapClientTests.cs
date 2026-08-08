using System.Net;
using System.Text;
using AISO.Domain.SalesOrders;
using AISO.SapIntegration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AISO.UnitTests;

public class SapClientTests
{
    private static SapClient CreateClient(HttpStatusCode status, string jsonBody, out StubHttpMessageHandler handler)
    {
        handler = new StubHttpMessageHandler(status, jsonBody);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://sap.test/")
        };
        return new SapClient(httpClient, new StubTokenManager(), NullLogger<SapClient>.Instance);
    }

    [Fact]
    public async Task ReleaseOrder_WhenBodyHasNoSoNumber_FallsBackToRequestedNumber()
    {
        // RAP action returns only %tky, so the body carries no SoNumber.
        var client = CreateClient(HttpStatusCode.OK, "{\"OverallStatus\":\"A\"}", out _);

        var result = await client.ReleaseOrderAsync("9", "DEV-249");

        Assert.Equal("0000000009", result.SoNumber);
    }

    [Fact]
    public async Task RejectOrder_RefetchesAndMapsCancelledFromIsCancelledFlag()
    {
        var handler = new StubHttpMessageHandler(
            (HttpStatusCode.OK, "{}"), // rejectOrder POST
            (HttpStatusCode.OK, "{\"SoNumber\":\"11\",\"Customer\":\"1000\",\"OverallStatus\":\"A\",\"IsCancelled\":\"X\"}"),
            (HttpStatusCode.OK, "{\"value\":[]}"));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sap.test/") };
        var client = new SapClient(httpClient, new StubTokenManager(), NullLogger<SapClient>.Instance);

        var result = await client.RejectOrderAsync("11", "03", "DEV-249");

        Assert.Equal("0000000011", result.SoNumber);
        Assert.Equal(SalesOrderStatus.Cancelled, result.Status);
        Assert.Contains("rejectOrder", handler.RequestUris[0]);
        Assert.Contains("SalesOrder('0000000011')", handler.RequestUris[1]);
    }

    [Fact]
    public async Task RejectOrder_RefetchesAndMapsCancelledFromItemRejectionRsn()
    {
        var handler = new StubHttpMessageHandler(
            (HttpStatusCode.OK, "{}"), // rejectOrder POST
            (HttpStatusCode.OK, "{\"SoNumber\":\"12\",\"Customer\":\"1000\",\"OverallStatus\":\"A\",\"IsCancelled\":\"\"}"),
            (HttpStatusCode.OK,
                "{\"value\":[{\"SoNumber\":\"0000000012\",\"ItemNo\":\"000010\",\"Material\":\"M1\",\"OrderQty\":1,\"RejectionRsn\":\"02\"}]}"));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sap.test/") };
        var client = new SapClient(httpClient, new StubTokenManager(), NullLogger<SapClient>.Instance);

        var result = await client.RejectOrderAsync("12", "02", "DEV-249");

        Assert.Equal("0000000012", result.SoNumber);
        Assert.Equal(SalesOrderStatus.Cancelled, result.Status);
        Assert.Contains("rejectOrder", handler.RequestUris[0]);
        Assert.Contains("SalesOrder('0000000012')", handler.RequestUris[1]);
        Assert.Contains("SalesOrderItem", handler.RequestUris[2]);
    }

    [Fact]
    public async Task RejectOrder_WhenRefetchMissing_FallsBackToCancelled()
    {
        var handler = new StubHttpMessageHandler(
            (HttpStatusCode.OK, "{\"SoNumber\":\"0000000012\"}"),
            (HttpStatusCode.NotFound, ""));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sap.test/") };
        var client = new SapClient(httpClient, new StubTokenManager(), NullLogger<SapClient>.Instance);

        var result = await client.RejectOrderAsync("12", "02", "DEV-249");

        Assert.Equal("0000000012", result.SoNumber);
        Assert.Equal(SalesOrderStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task GetSalesOrderById_MapsCancelledWhenAllItemsHaveRejectionRsn()
    {
        var handler = new StubHttpMessageHandler(
            (HttpStatusCode.OK, "{\"SoNumber\":\"9\",\"Customer\":\"1000\",\"OverallStatus\":\"A\",\"IsCancelled\":\"\"}"),
            (HttpStatusCode.OK,
                "{\"value\":[{\"ItemNo\":\"10\",\"Material\":\"A\",\"RejectionRsn\":\"03\"},{\"ItemNo\":\"20\",\"Material\":\"B\",\"RejectionRsn\":\"02\"}]}"));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sap.test/") };
        var client = new SapClient(httpClient, new StubTokenManager(), NullLogger<SapClient>.Instance);

        var result = await client.GetSalesOrderByIdAsync("9");

        Assert.Equal(SalesOrderStatus.Cancelled, result!.Status);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task ForwardOrder_WhenBodyHasNoSoNumber_FallsBackToRequestedNumber()
    {
        var client = CreateClient(HttpStatusCode.OK, "{}", out _);

        var result = await client.ForwardOrderAsync("42", "DEV-300", "DEV-249");

        Assert.Equal("0000000042", result.SoNumber);
    }

    [Fact]
    public async Task ReleaseOrder_WhenSapReturnsShortDump_ThrowsFriendlyMessage()
    {
        var body = "{\"error\":{\"code\":\"RAISE_SHORTDUMP\",\"message\":\"boom\"}}";
        var client = CreateClient(HttpStatusCode.InternalServerError, body, out _);

        var ex = await Assert.ThrowsAsync<SapODataException>(
            () => client.ReleaseOrderAsync("9", "DEV-249"));

        Assert.Equal(500, ex.HttpStatusCode);
        Assert.Contains("ABAP Short Dump", ex.Message);
    }

    [Fact]
    public async Task RejectOrder_WhenSapReturnsBusinessError_SurfacesSapMessage()
    {
        var body = "{\"error\":{\"code\":\"SY530\",\"message\":\"No authorization for maintaining sales documents\"}}";
        var client = CreateClient(HttpStatusCode.BadRequest, body, out _);

        var ex = await Assert.ThrowsAsync<SapODataException>(
            () => client.RejectOrderAsync("9", "R1", "DEV-249"));

        Assert.Equal("No authorization for maintaining sales documents", ex.Message);
    }

    [Fact]
    public async Task RejectOrder_WhenSapReturnsObjectMessage_SurfacesValue()
    {
        var body =
            "{\"error\":{\"code\":\"/IWBEP/CX_MGW_BUSI_EXCEPTION\",\"message\":{\"lang\":\"en\",\"value\":\"Order has invalid material master data\"}}}";
        var client = CreateClient(HttpStatusCode.BadRequest, body, out _);

        var ex = await Assert.ThrowsAsync<SapODataException>(
            () => client.RejectOrderAsync("9", "02", "DEV-249"));

        Assert.Equal("Order has invalid material master data", ex.Message);
    }

    [Fact]
    public async Task RejectOrder_WhenSapReturnsErrorDetails_PrefersDetailMessage()
    {
        var body =
            "{\"error\":{\"code\":\"GENERIC\",\"message\":\"Exception raised\",\"innererror\":{\"errordetails\":[{\"code\":\"V1\",\"message\":\"The material in item 000010 cannot be changed\",\"severity\":\"error\"}]}}}";
        var client = CreateClient(HttpStatusCode.BadRequest, body, out _);

        var ex = await Assert.ThrowsAsync<SapODataException>(
            () => client.RejectOrderAsync("9", "02", "DEV-249"));

        Assert.Equal("The material in item 000010 cannot be changed", ex.Message);
    }

    [Fact]
    public async Task GetSalesOrderById_WhenNotFound_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound, "{}", out _);

        var result = await client.GetSalesOrderByIdAsync("9");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSalesOrderById_MapsStatusAndPadsNumber()
    {
        var body = "{\"SoNumber\":\"9\",\"Customer\":\"1000\",\"CustomerName\":\"Philly Bikes\",\"OverallStatus\":\"C\",\"Currency\":\"EUR\"}";
        var client = CreateClient(HttpStatusCode.OK, body, out _);

        var result = await client.GetSalesOrderByIdAsync("9");

        Assert.NotNull(result);
        Assert.Equal("0000000009", result!.SoNumber);
        Assert.Equal(SalesOrderStatus.Delivered, result.Status);
        Assert.Equal("EUR", result.Currency);
        Assert.Equal("Philly Bikes", result.CustomerName);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetSalesOrderById_LoadsItemsViaSideRequest()
    {
        var headerBody =
            "{\"SoNumber\":\"15\",\"Customer\":\"1000\",\"CustomerName\":\"Acme\",\"OverallStatus\":\"A\",\"Currency\":\"USD\",\"NetValue\":1200}";
        var itemsBody =
            "{\"value\":[{\"SoNumber\":\"0000000015\",\"ItemNo\":\"000010\",\"Material\":\"TG11\",\"MaterialName\":\"Touring Bike\",\"Plant\":\"1010\",\"OrderQty\":2,\"Unit\":\"PC\",\"NetValue\":800,\"Currency\":\"USD\"},{\"SoNumber\":\"0000000015\",\"ItemNo\":\"000020\",\"Material\":\"TG12\",\"OrderQty\":1,\"Unit\":\"PC\",\"NetValue\":400,\"Currency\":\"USD\"}]}";

        var handler = new StubHttpMessageHandler(
            (HttpStatusCode.OK, headerBody),
            (HttpStatusCode.OK, itemsBody));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sap.test/") };
        var client = new SapClient(httpClient, new StubTokenManager(), NullLogger<SapClient>.Instance);

        var result = await client.GetSalesOrderByIdAsync("15");

        Assert.NotNull(result);
        Assert.Equal("0000000015", result!.SoNumber);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("000010", result.Items[0].ItemNumber);
        Assert.Equal("TG11", result.Items[0].Material);
        Assert.Equal("Touring Bike", result.Items[0].Description);
        Assert.Equal(2m, result.Items[0].Quantity);
        Assert.Equal("PC", result.Items[0].Unit);
        Assert.Equal(800m, result.Items[0].NetValue);
        Assert.Equal("000020", result.Items[1].ItemNumber);
        Assert.Equal("TG12", result.Items[1].Description); // fallback when MaterialName missing

        Assert.Equal(2, handler.RequestUris.Count);
        Assert.Contains("SalesOrder('0000000015')", handler.RequestUris[0]);
        var itemsUrl = Uri.UnescapeDataString(handler.RequestUris[1]);
        Assert.Contains("SalesOrderItem", itemsUrl);
        Assert.Contains("SoNumber eq '0000000015'", itemsUrl);
        Assert.Contains("sap-client=324", itemsUrl);
    }

    [Fact]
    public async Task GetSalesOrderById_WhenItemsRequestFails_ReturnsHeaderWithoutItems()
    {
        var headerBody =
            "{\"SoNumber\":\"15\",\"Customer\":\"1000\",\"OverallStatus\":\"A\",\"Currency\":\"USD\"}";
        var handler = new StubHttpMessageHandler(
            (HttpStatusCode.OK, headerBody),
            (HttpStatusCode.InternalServerError, "{\"error\":{\"message\":\"boom\"}}"));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sap.test/") };
        var client = new SapClient(httpClient, new StubTokenManager(), NullLogger<SapClient>.Instance);

        var result = await client.GetSalesOrderByIdAsync("15");

        Assert.NotNull(result);
        Assert.Equal("0000000015", result!.SoNumber);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task CreateSalesOrder_PostsAction_WithRequestingUser_AndRefetches()
    {
        var handler = new StubHttpMessageHandler(
            (HttpStatusCode.OK, "{\"SoNumber\":\"0000001888\"}"),
            (HttpStatusCode.OK,
                "{\"SoNumber\":\"0000001888\",\"Customer\":\"1000\",\"SalesOrg\":\"FU24\",\"OverallStatus\":\"A\",\"Currency\":\"VND\"}"),
            (HttpStatusCode.OK, "{\"value\":[]}"));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sap.test/") };
        var client = new SapClient(httpClient, new StubTokenManager(), NullLogger<SapClient>.Instance);

        var result = await client.CreateSalesOrderAsync(new CreateSalesOrderDto
        {
            DocType = "ZOR",
            SalesOrg = "FU24",
            DistChannel = "FU",
            Division = "FS",
            Customer = "1000",
            Currency = "VND",
            RequestingSapUser = "DEV-024",
            Items = new[]
            {
                new CreateSalesOrderItemDto
                {
                    Material = "MAT-1",
                    Plant = "1010",
                    OrderQty = 1,
                    Unit = "PC"
                }
            }
        });

        Assert.Equal("0000001888", result.SoNumber);
        Assert.Contains(handler.RequestUris, u => u.Contains("createSalesOrder", StringComparison.Ordinal));
        Assert.Contains("REQUESTING_TEAMS_USER", handler.RequestBodies[0] ?? "");
        Assert.Contains("DEV-024", handler.RequestBodies[0] ?? "");
        Assert.Contains("\"CUSTOMER\":\"0000001000\"", handler.RequestBodies[0] ?? "");
        Assert.Contains(handler.RequestUris, u => u.Contains("SalesOrder('0000001888')", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("100323", "0000100323")]
    [InlineData("0000100323", "0000100323")]
    [InlineData("1000", "0000001000")]
    [InlineData("USCU_ABC1", "USCU_ABC1")]
    public void FormatCustomerNumber_PadsNumericIds(string input, string expected)
    {
        Assert.Equal(expected, SapClient.FormatCustomerNumber(input));
    }

    [Fact]
    public async Task SyncUserRole_PostsStaticAction_WithAdminAndTarget()
    {
        var client = CreateClient(HttpStatusCode.OK, "{}", out var handler);

        await client.SyncUserRoleAsync("DEV-024", "MANAGER", "FU24", "DEV-001");

        Assert.Contains(handler.RequestUris, u => u.Contains("syncUserRole", StringComparison.Ordinal));
        Assert.Contains(handler.RequestUris, u => u.Contains("UserRole/", StringComparison.Ordinal));
        Assert.Contains("DEV-024", handler.RequestBodies[0] ?? "");
        Assert.Contains("MANAGER", handler.RequestBodies[0] ?? "");
        Assert.Contains("FU24", handler.RequestBodies[0] ?? "");
        Assert.Contains("DEV-001", handler.RequestBodies[0] ?? "");
    }

    [Fact]
    public async Task ApproveOrder_PostsApproveOrderAction_WithSapUserParam()
    {
        var client = CreateClient(HttpStatusCode.OK, "{}", out var handler);

        var result = await client.ApproveOrderAsync("9", "DEV-249");

        Assert.Equal("0000000009", result.SoNumber);
        Assert.Contains(handler.RequestUris, u => u.Contains("approveOrder", StringComparison.Ordinal));
        Assert.Contains("REQUESTING_TEAMS_USER", handler.RequestBodies[0] ?? "");
        Assert.Contains("DEV-249", handler.RequestBodies[0] ?? "");
    }

    [Fact]
    public async Task ForceRelease_PostsOverrideReason()
    {
        var client = CreateClient(HttpStatusCode.OK, "{}", out var handler);

        var result = await client.ForceReleaseAsync("42", "DEV-001", "emergency unlock");

        Assert.Equal("0000000042", result.SoNumber);
        Assert.Contains(handler.RequestUris, u => u.Contains("forceRelease", StringComparison.Ordinal));
        Assert.Contains("OVERRIDE_REASON", handler.RequestBodies[0] ?? "");
        Assert.Contains("emergency unlock", handler.RequestBodies[0] ?? "");
    }

    [Fact]
    public async Task GetSalesOrderById_WhenCustomerNameMissing_UsesNotAvailableLabel()
    {
        var body = "{\"SoNumber\":\"9\",\"Customer\":\"1000\",\"OverallStatus\":\"A\"}";
        var client = CreateClient(HttpStatusCode.OK, body, out _);

        var result = await client.GetSalesOrderByIdAsync("9");

        Assert.Equal("N/A", result!.CustomerName);
    }

    [Fact]
    public async Task GetSalesOrders_ByDefault_FiltersHasInvalidMaterialEmpty()
    {
        var body = "{\"value\":[]}";
        var client = CreateClient(HttpStatusCode.OK, body, out var handler);

        await client.GetSalesOrdersAsync(new SalesOrdersQuery { Top = 10 });

        var decoded = Uri.UnescapeDataString(handler.LastRequestUri!);
        Assert.Contains("HasInvalidMaterial eq ''", decoded);
    }

    [Fact]
    public async Task GetSalesOrders_WhenIncludeInvalid_OmitsHasInvalidMaterialFilter()
    {
        var body = "{\"value\":[]}";
        var client = CreateClient(HttpStatusCode.OK, body, out var handler);

        await client.GetSalesOrdersAsync(new SalesOrdersQuery
        {
            ExcludeInvalidMaterials = false
        });

        var decoded = Uri.UnescapeDataString(handler.LastRequestUri!);
        Assert.DoesNotContain("HasInvalidMaterial", decoded);
    }

    [Fact]
    public async Task GetSalesOrderById_MapsHasInvalidMaterial()
    {
        var body = "{\"SoNumber\":\"9\",\"Customer\":\"1000\",\"OverallStatus\":\"A\",\"HasInvalidMaterial\":\"X\"}";
        var client = CreateClient(HttpStatusCode.OK, body, out _);

        var result = await client.GetSalesOrderByIdAsync("9");

        Assert.True(result!.HasInvalidMaterial);
    }

    [Fact]
    public async Task GetSalesOrders_WhenCustomerId_FiltersCustomerEq()
    {
        var body = "{\"value\":[]}";
        var client = CreateClient(HttpStatusCode.OK, body, out var handler);

        await client.GetSalesOrdersAsync(new SalesOrdersQuery
        {
            CustomerIdOrName = "1000",
            Top = 10
        });

        var decoded = Uri.UnescapeDataString(handler.LastRequestUri!);
        Assert.Contains("Customer eq '1000'", decoded);
        Assert.DoesNotContain("contains(CustomerName", decoded);
    }

    [Fact]
    public async Task GetSalesOrders_WhenCustomerName_FiltersContainsCustomerName()
    {
        var body = "{\"value\":[]}";
        var client = CreateClient(HttpStatusCode.OK, body, out var handler);

        await client.GetSalesOrdersAsync(new SalesOrdersQuery
        {
            CustomerIdOrName = "Philly Bikes",
            Top = 10
        });

        var decoded = Uri.UnescapeDataString(handler.LastRequestUri!);
        Assert.Contains("contains(CustomerName,'Philly Bikes')", decoded);
        Assert.DoesNotContain("Customer eq", decoded);
    }

    [Fact]
    public async Task GetSalesOrders_WhenCustomerNameHasQuote_EscapesODataLiteral()
    {
        var body = "{\"value\":[]}";
        var client = CreateClient(HttpStatusCode.OK, body, out var handler);

        await client.GetSalesOrdersAsync(new SalesOrdersQuery
        {
            CustomerIdOrName = "O'Brien Cycles"
        });

        var decoded = Uri.UnescapeDataString(handler.LastRequestUri!);
        Assert.Contains("contains(CustomerName,'O''Brien Cycles')", decoded);
    }

    [Theory]
    [InlineData("1000", true)]
    [InlineData("0000100001", true)]
    [InlineData("USCU_ABC1", true)]
    [InlineData("CUST-5002", true)]
    [InlineData("Philly Bikes", false)]
    [InlineData("Philly", false)]
    [InlineData("Sunrise Corp", false)]
    public void LooksLikeCustomerId_ClassifiesIdVsName(string value, bool expectedId)
    {
        Assert.Equal(expectedId, SapClient.LooksLikeCustomerId(value));
    }

    [Fact]
    public async Task GetSalesOrders_WhenStatusOpen_FiltersOverallStatusA()
    {
        var body = "{\"value\":[]}";
        var client = CreateClient(HttpStatusCode.OK, body, out var handler);

        await client.GetSalesOrdersAsync(new SalesOrdersQuery
        {
            Status = SalesOrderStatus.Open,
            Top = 10
        });

        Assert.NotNull(handler.LastRequestUri);
        var decoded = Uri.UnescapeDataString(handler.LastRequestUri!);
        Assert.Contains("OverallStatus eq 'A'", decoded);
    }

    [Fact]
    public async Task GetSalesOrders_WhenStatusDelivered_FiltersOverallStatusC()
    {
        var body = "{\"value\":[]}";
        var client = CreateClient(HttpStatusCode.OK, body, out var handler);

        await client.GetSalesOrdersAsync(new SalesOrdersQuery
        {
            Status = SalesOrderStatus.Delivered
        });

        var decoded = Uri.UnescapeDataString(handler.LastRequestUri!);
        Assert.Contains("OverallStatus eq 'C'", decoded);
    }

    [Fact]
    public async Task GetSalesOrders_WhenStatusCancelled_FiltersIsCancelled()
    {
        var body = "{\"value\":[]}";
        var client = CreateClient(HttpStatusCode.OK, body, out var handler);

        await client.GetSalesOrdersAsync(new SalesOrdersQuery
        {
            Status = SalesOrderStatus.Cancelled
        });

        var decoded = Uri.UnescapeDataString(handler.LastRequestUri!);
        Assert.Contains("IsCancelled eq 'X'", decoded);
    }

    [Fact]
    public async Task GetSalesOrderById_MapsCancelledWhenIsCancelledX()
    {
        var body = "{\"SoNumber\":\"9\",\"Customer\":\"1000\",\"OverallStatus\":\"A\",\"IsCancelled\":\"X\"}";
        var client = CreateClient(HttpStatusCode.OK, body, out _);

        var result = await client.GetSalesOrderByIdAsync("9");

        Assert.Equal(SalesOrderStatus.Cancelled, result!.Status);
    }

    [Fact]
    public async Task GetSalesOrderById_MapsOwnerSapUser()
    {
        var body = "{\"SoNumber\":\"9\",\"Customer\":\"1000\",\"OverallStatus\":\"A\",\"OwnerSapUser\":\"DEV-249\"}";
        var client = CreateClient(HttpStatusCode.OK, body, out _);

        var result = await client.GetSalesOrderByIdAsync("9");

        Assert.Equal("DEV-249", result!.OwnerSapUser);
    }

    [Theory]
    [InlineData(SalesOrderStatus.Open, "OverallStatus eq 'A'")]
    [InlineData(SalesOrderStatus.PartiallyDelivered, "OverallStatus eq 'B'")]
    [InlineData(SalesOrderStatus.Delivered, "OverallStatus eq 'C'")]
    [InlineData(SalesOrderStatus.Blocked, "DeliveryBlock ne ''")]
    [InlineData(SalesOrderStatus.Invoiced, "BillingStatus eq 'C'")]
    [InlineData(SalesOrderStatus.Cancelled, "IsCancelled eq 'X'")]
    public void ApplyStatusFilter_MapsDomainStatusToSapFilter(SalesOrderStatus status, string expectedFragment)
    {
        var builder = new ODataQueryBuilder("SalesOrder");
        SapClient.ApplyStatusFilter(builder, status);

        Assert.Contains(Uri.EscapeDataString(expectedFragment), builder.Build());
    }

    private sealed class StubTokenManager : ISapTokenManager
    {
        public Task<SapAuthContext> GetAuthContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SapAuthContext("csrf-token", "session-cookie"));

        public Task<SapAuthContext> RefreshAuthContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SapAuthContext("csrf-token", "session-cookie"));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses;

        public StubHttpMessageHandler(HttpStatusCode status, string body)
            : this((status, body))
        {
        }

        public StubHttpMessageHandler(params (HttpStatusCode Status, string Body)[] responses)
        {
            _responses = new Queue<(HttpStatusCode, string)>(responses);
        }

        public string? LastRequestUri { get; private set; }
        public string? LastRequestBody { get; private set; }
        public List<string> RequestUris { get; } = new();
        public List<string?> RequestBodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            if (LastRequestUri is not null)
                RequestUris.Add(LastRequestUri);

            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(LastRequestBody);

            var (status, body) = _responses.Count > 0
                ? _responses.Dequeue()
                : (HttpStatusCode.OK, "{\"value\":[]}");

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
