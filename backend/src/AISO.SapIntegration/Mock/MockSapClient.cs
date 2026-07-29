using AISO.Domain.Kpi;
using AISO.Domain.SalesOrders;
using Microsoft.Extensions.Logging;

namespace AISO.SapIntegration.Mock;

public sealed class MockSapClient : ISapClient
{
    private readonly ILogger<MockSapClient>? _logger;
    private readonly Dictionary<string, string> _owners = new(StringComparer.OrdinalIgnoreCase);

    public MockSapClient(ILogger<MockSapClient>? logger = null)
    {
        _logger = logger;
    }

    private static readonly IReadOnlyList<SalesOrder> SeedData = new List<SalesOrder>
    {
        new()
        {
            SoNumber = "0000005001",
            CustomerId = "1000",
            CustomerName = "Philly Bikes",
            OrderDate = new DateOnly(2026, 5, 28),
            NetValue = 15_750m,
            Currency = "USD",
            SalesOrg = "UE00",
            Status = SalesOrderStatus.Open,
            Items = new List<SalesOrderItem>
            {
                new()
                {
                    ItemNumber = "00010",
                    Material = "DXTR1000",
                    Description = "Deluxe Touring Bike (Black)",
                    Quantity = 5m,
                    Unit = "EA",
                    NetValue = 12_500m,
                },
                new()
                {
                    ItemNumber = "00020",
                    Material = "WDFR1000",
                    Description = "Water Bottle (Front)",
                    Quantity = 50m,
                    Unit = "EA",
                    NetValue = 3_250m,
                },
            },
        },
        new()
        {
            SoNumber = "0000005002",
            CustomerId = "1003",
            CustomerName = "Beantown Bikes",
            OrderDate = new DateOnly(2026, 5, 30),
            NetValue = 8_400m,
            Currency = "USD",
            SalesOrg = "UE00",
            Status = SalesOrderStatus.Delivered,
            Items = new List<SalesOrderItem>
            {
                new()
                {
                    ItemNumber = "00010",
                    Material = "DXTR2000",
                    Description = "Deluxe Touring Bike (Silver)",
                    Quantity = 3m,
                    Unit = "EA",
                    NetValue = 8_400m,
                },
            },
        },
        new()
        {
            SoNumber = "0000005003",
            CustomerId = "2000",
            CustomerName = "Berlin Bikes",
            OrderDate = new DateOnly(2026, 6, 1),
            NetValue = 22_300m,
            Currency = "EUR",
            SalesOrg = "DN00",
            Status = SalesOrderStatus.Blocked,
            Items = new List<SalesOrderItem>
            {
                new()
                {
                    ItemNumber = "00010",
                    Material = "PRTR1000",
                    Description = "Professional Touring Bike (Black)",
                    Quantity = 8m,
                    Unit = "EA",
                    NetValue = 22_300m,
                },
            },
        },
        new()
        {
            SoNumber = "0000005004",
            CustomerId = "3000",
            CustomerName = "Munich Bikes",
            OrderDate = new DateOnly(2026, 6, 3),
            NetValue = 5_600m,
            Currency = "EUR",
            SalesOrg = "DS00",
            Status = SalesOrderStatus.PartiallyDelivered,
            Items = new List<SalesOrderItem>
            {
                new()
                {
                    ItemNumber = "00010",
                    Material = "ORWN1000",
                    Description = "Off Road Bike",
                    Quantity = 4m,
                    Unit = "EA",
                    NetValue = 5_600m,
                },
            },
        },
        new()
        {
            SoNumber = "0000005005",
            CustomerId = "1004",
            CustomerName = "Rocky Mountain Bikes",
            OrderDate = new DateOnly(2026, 6, 5),
            NetValue = 11_200m,
            Currency = "USD",
            SalesOrg = "UW00",
            Status = SalesOrderStatus.Open,
            Items = new List<SalesOrderItem>
            {
                new()
                {
                    ItemNumber = "00010",
                    Material = "DXTR1000",
                    Description = "Deluxe Touring Bike (Black)",
                    Quantity = 4m,
                    Unit = "EA",
                    NetValue = 10_000m,
                },
                new()
                {
                    ItemNumber = "00020",
                    Material = "WDFR1000",
                    Description = "Water Bottle (Front)",
                    Quantity = 24m,
                    Unit = "EA",
                    NetValue = 1_200m,
                },
            },
        },
    };

    public Task<IReadOnlyList<SalesOrder>> GetSalesOrdersAsync(SalesOrdersQuery query, CancellationToken ct = default)
    {
        _logger?.LogDebug("MockSapClient.GetSalesOrdersAsync: customer={Customer}, salesOrg={SalesOrg}, top={Top}",
            query.CustomerIdOrName, query.SalesOrg, query.Top);

        IEnumerable<SalesOrder> q = SeedData;
        if (!string.IsNullOrWhiteSpace(query.CustomerIdOrName))
        {
            var needle = query.CustomerIdOrName;
            q = q.Where(o => o.CustomerId == needle || o.CustomerName.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.SalesOrg))
            q = q.Where(o => o.SalesOrg == query.SalesOrg);
        if (query.FromDate.HasValue)
            q = q.Where(o => o.OrderDate >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            q = q.Where(o => o.OrderDate <= query.ToDate.Value);
        if (query.Status.HasValue)
            q = q.Where(o => o.Status == query.Status.Value);

        var result = q.OrderByDescending(o => o.OrderDate).Take(Math.Clamp(query.Top, 1, 50)).ToList();
        return Task.FromResult<IReadOnlyList<SalesOrder>>(result);
    }

    public Task<SalesOrder?> GetSalesOrderByIdAsync(string soNumber, CancellationToken ct = default)
    {
        _logger?.LogDebug("MockSapClient.GetSalesOrderByIdAsync: {SoNumber}", soNumber);
        var order = SeedData.FirstOrDefault(x => x.SoNumber == soNumber);
        if (order is null)
            return Task.FromResult<SalesOrder?>(null);

        _owners.TryGetValue(soNumber, out var owner);
        return Task.FromResult<SalesOrder?>(order with { OwnerSapUser = owner });
    }

    public Task<SalesOrder> CreateSalesOrderAsync(CreateSalesOrderDto dto, CancellationToken ct = default)
    {
        return Task.FromResult(new SalesOrder
        {
            SoNumber = "9999999999",
            CustomerId = dto.Customer,
            CustomerName = "Mock Customer",
            SalesOrg = dto.SalesOrg,
            OrderDate = DateOnly.FromDateTime(DateTime.Now),
            NetValue = 1000m,
            Currency = dto.Currency,
            Status = SalesOrderStatus.Open,
            Items = Array.Empty<SalesOrderItem>(),
        });
    }

    public Task<SalesOrder> UpdateReferenceAsync(string soNumber, string newReference, string requestingSapUser, CancellationToken ct = default)
    {
        var order = SeedData.FirstOrDefault(x => x.SoNumber == soNumber)
            ?? throw new InvalidOperationException($"Order {soNumber} not found.");
        return Task.FromResult(order);
    }

    public Task<SalesOrder> RejectOrderAsync(string soNumber, string rejectionCode, string requestingTeamsUser, CancellationToken ct = default)
    {
        var order = SeedData.FirstOrDefault(x => x.SoNumber == soNumber)
            ?? throw new InvalidOperationException($"Order {soNumber} not found.");
        return Task.FromResult(order with { Status = SalesOrderStatus.Cancelled });
    }

    public Task<SalesOrder> ReleaseOrderAsync(string soNumber, string requestingTeamsUser, CancellationToken ct = default)
    {
        var order = SeedData.FirstOrDefault(x => x.SoNumber == soNumber)
            ?? throw new InvalidOperationException($"Order {soNumber} not found.");
        return Task.FromResult(order with { Status = SalesOrderStatus.Open });
    }

    public Task<SalesOrder> ApproveOrderAsync(string soNumber, string requestingSapUser, CancellationToken ct = default)
        => ReleaseOrderAsync(soNumber, requestingSapUser, ct);

    public Task<SalesOrder> RejectApprovalAsync(string soNumber, string requestingSapUser, CancellationToken ct = default)
    {
        var order = SeedData.FirstOrDefault(x => x.SoNumber == soNumber)
            ?? throw new InvalidOperationException($"Order {soNumber} not found.");
        return Task.FromResult(order);
    }

    public Task<SalesOrder> ForceReleaseAsync(
        string soNumber,
        string requestingSapUser,
        string overrideReason,
        CancellationToken ct = default)
        => ReleaseOrderAsync(soNumber, requestingSapUser, ct);

    public Task<SalesOrder> ForceCancelAsync(
        string soNumber,
        string requestingSapUser,
        string overrideReason,
        CancellationToken ct = default)
        => RejectOrderAsync(soNumber, "02", requestingSapUser, ct);

    public Task<SalesOrder> ReassignOwnerAsync(
        string soNumber,
        string newOwnerSapUser,
        string requestingSapUser,
        CancellationToken ct = default)
    {
        var order = SeedData.FirstOrDefault(x => x.SoNumber == soNumber)
            ?? throw new InvalidOperationException($"Order {soNumber} not found.");
        _owners[soNumber] = newOwnerSapUser;
        return Task.FromResult(order with { OwnerSapUser = newOwnerSapUser });
    }

    public Task<SalesOrder> ForwardOrderAsync(
        string soNumber,
        string forwardToUser,
        string requestingTeamsUser,
        CancellationToken ct = default,
        string? remarks = null)
    {
        var order = SeedData.FirstOrDefault(x => x.SoNumber == soNumber)
            ?? throw new InvalidOperationException($"Order {soNumber} not found.");

        if (_owners.TryGetValue(soNumber, out var owner)
            && !string.Equals(owner, requestingTeamsUser, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Order owned by another user");
        }

        _owners[soNumber] = forwardToUser;
        return Task.FromResult(order with { OwnerSapUser = forwardToUser });
    }

    public Task<KpiSummary> GetKpiSummaryAsync(KpiSummaryQuery query, CancellationToken ct = default)
    {
        _logger?.LogDebug("MockSapClient.GetKpiSummaryAsync called");
        var summary = new KpiSummary
        {
            TotalRevenue = 63_250m,
            Currency = "USD",
            TotalOrders = SeedData.Count,
            OpenOrders = SeedData.Count(o => o.Status == SalesOrderStatus.Open),
            DeliveredOrders = SeedData.Count(o => o.Status == SalesOrderStatus.Delivered),
            OverdueOrders = 1,
            FulfillmentRate = 80.0m,
            CancellationRate = 5.0m,
            Period = "Mock period",
            SalesOrg = query.SalesOrg,
            Granularity = query.Granularity,
            RevenueTimeSeries = new List<KpiDataPoint>
            {
                new("May-26", 24_150m),
                new("Jun-26", 39_100m),
            },
        };
        return Task.FromResult(summary);
    }

    public Task<IReadOnlyList<KpiByCustomer>> GetKpiByCustomerAsync(KpiByCustomerQuery query, CancellationToken ct = default)
    {
        _logger?.LogDebug("MockSapClient.GetKpiByCustomerAsync called");
        IReadOnlyList<KpiByCustomer> result = SeedData
            .GroupBy(o => new { o.CustomerId, o.CustomerName })
            .Select(g => new KpiByCustomer
            {
                CustomerId = g.Key.CustomerId,
                CustomerName = g.Key.CustomerName,
                Revenue = g.Sum(o => o.NetValue),
                Currency = g.First().Currency,
                OrderCount = g.Count(),
                FulfillmentRate = g.Count() > 0
                    ? g.Count(o => o.Status == SalesOrderStatus.Delivered) * 100m / g.Count()
                    : 0,
            })
            .OrderByDescending(c => c.Revenue)
            .Take(query.Top)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<KpiByProduct>> GetKpiByProductAsync(KpiByProductQuery query, CancellationToken ct = default)
    {
        _logger?.LogDebug("MockSapClient.GetKpiByProductAsync called");
        IReadOnlyList<KpiByProduct> result = SeedData
            .SelectMany(o => o.Items)
            .GroupBy(i => new { i.Material, i.Description })
            .Select(g => new KpiByProduct
            {
                MaterialId = g.Key.Material,
                MaterialName = g.Key.Description,
                Revenue = g.Sum(i => i.NetValue),
                Currency = "USD",
                TotalQty = g.Sum(i => i.Quantity),
                Unit = "EA",
                OrderCount = g.Count(),
            })
            .OrderByDescending(p => p.Revenue)
            .Take(query.Top)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<OverdueOrder>> GetOverdueOrdersAsync(OverdueOrdersQuery query, CancellationToken ct = default)
    {
        _logger?.LogDebug("MockSapClient.GetOverdueOrdersAsync called");
        var today = DateOnly.FromDateTime(DateTime.Today);
        IReadOnlyList<OverdueOrder> result = SeedData
            .Where(o => o.Status == SalesOrderStatus.Open)
            .Select(o => new OverdueOrder
            {
                SoNumber = o.SoNumber,
                CustomerId = o.CustomerId,
                CustomerName = o.CustomerName,
                ScheduledDeliveryDate = o.OrderDate.AddDays(14),
                DaysPastDue = Math.Max(0, today.DayNumber - o.OrderDate.AddDays(14).DayNumber),
                NetValue = o.NetValue,
                Currency = o.Currency,
                SalesOrg = o.SalesOrg,
            })
            .Where(o => o.DaysPastDue > 0)
            .OrderByDescending(o => o.DaysPastDue)
            .Take(query.Top)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<bool?> SapUserExistsAsync(string sapUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sapUserId))
            return Task.FromResult<bool?>(false);

        var normalized = sapUserId.Trim().ToUpperInvariant();
        // Demo seed IDs + DEV-* pattern used in AISO landscape.
        var known = normalized is "DEV-024" or "DEV-249" or "DEV-230"
            || normalized.StartsWith("DEV-", StringComparison.Ordinal);
        return Task.FromResult<bool?>(known);
    }
}
