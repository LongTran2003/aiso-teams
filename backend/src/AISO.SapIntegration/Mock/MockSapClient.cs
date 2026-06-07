using AISO.Domain.SalesOrders;

namespace AISO.SapIntegration.Mock;

/// <summary>
/// In-memory implementation of <see cref="ISapClient"/> for Sprint 2 development.
/// Replaced in Sprint 3 by a real OData client calling SAP via Cloud Connector.
/// Seed data follows the Global Bike sample dataset (UCC S40 client 324).
/// </summary>
public sealed class MockSapClient : ISapClient
{
    private static readonly IReadOnlyList<SalesOrder> SeedData = new List<SalesOrder>
    {
        new()
        {
            SoNumber = "0000005001", CustomerId = "1000", CustomerName = "Philly Bikes",
            OrderDate = new DateOnly(2026, 5, 28), NetValue = 15_750m, Currency = "USD",
            SalesOrg = "UE00", Status = SalesOrderStatus.Open,
            Items = new List<SalesOrderItem>
            {
                new()
                {
                    ItemNumber = "00010", Material = "DXTR1000",
                    Description = "Deluxe Touring Bike (Black)",
                    Quantity = 5m, Unit = "EA", NetValue = 12_500m
                },
                new()
                {
                    ItemNumber = "00020", Material = "WDFR1000",
                    Description = "Water Bottle (Front)",
                    Quantity = 50m, Unit = "EA", NetValue = 3_250m
                }
            }
        },
        new()
        {
            SoNumber = "0000005002", CustomerId = "1003", CustomerName = "Beantown Bikes",
            OrderDate = new DateOnly(2026, 5, 30), NetValue = 8_400m, Currency = "USD",
            SalesOrg = "UE00", Status = SalesOrderStatus.Delivered,
            Items = new List<SalesOrderItem>
            {
                new()
                {
                    ItemNumber = "00010", Material = "DXTR2000",
                    Description = "Deluxe Touring Bike (Silver)",
                    Quantity = 3m, Unit = "EA", NetValue = 8_400m
                }
            }
        },
        new()
        {
            SoNumber = "0000005003", CustomerId = "2000", CustomerName = "Berlin Bikes",
            OrderDate = new DateOnly(2026, 6, 1), NetValue = 22_300m, Currency = "EUR",
            SalesOrg = "DN00", Status = SalesOrderStatus.Blocked,
            Items = new List<SalesOrderItem>
            {
                new()
                {
                    ItemNumber = "00010", Material = "PRTR1000",
                    Description = "Professional Touring Bike (Black)",
                    Quantity = 8m, Unit = "EA", NetValue = 22_300m
                }
            }
        },
        new()
        {
            SoNumber = "0000005004", CustomerId = "3000", CustomerName = "Munich Bikes",
            OrderDate = new DateOnly(2026, 6, 3), NetValue = 5_600m, Currency = "EUR",
            SalesOrg = "DS00", Status = SalesOrderStatus.PartiallyDelivered,
            Items = new List<SalesOrderItem>
            {
                new()
                {
                    ItemNumber = "00010", Material = "ORWN1000",
                    Description = "Off Road Bike",
                    Quantity = 4m, Unit = "EA", NetValue = 5_600m
                }
            }
        },
        new()
        {
            SoNumber = "0000005005", CustomerId = "1004", CustomerName = "Rocky Mountain Bikes",
            OrderDate = new DateOnly(2026, 6, 5), NetValue = 11_200m, Currency = "USD",
            SalesOrg = "UW00", Status = SalesOrderStatus.Open,
            Items = new List<SalesOrderItem>
            {
                new()
                {
                    ItemNumber = "00010", Material = "DXTR1000",
                    Description = "Deluxe Touring Bike (Black)",
                    Quantity = 4m, Unit = "EA", NetValue = 10_000m
                },
                new()
                {
                    ItemNumber = "00020", Material = "WDFR1000",
                    Description = "Water Bottle (Front)",
                    Quantity = 24m, Unit = "EA", NetValue = 1_200m
                }
            }
        }
    };

    public Task<IReadOnlyList<SalesOrder>> GetSalesOrdersAsync(
        SalesOrdersQuery query,
        CancellationToken ct = default)
    {
        IEnumerable<SalesOrder> q = SeedData;

        if (!string.IsNullOrWhiteSpace(query.CustomerIdOrName))
        {
            var needle = query.CustomerIdOrName;
            q = q.Where(o =>
                o.CustomerId == needle ||
                o.CustomerName.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.SalesOrg))
            q = q.Where(o => o.SalesOrg == query.SalesOrg);

        if (query.FromDate.HasValue)
            q = q.Where(o => o.OrderDate >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            q = q.Where(o => o.OrderDate <= query.ToDate.Value);

        if (query.Status.HasValue)
            q = q.Where(o => o.Status == query.Status.Value);

        var top = Math.Clamp(query.Top, 1, 50);
        var result = q.OrderByDescending(o => o.OrderDate).Take(top).ToList();
        return Task.FromResult<IReadOnlyList<SalesOrder>>(result);
    }

    public Task<SalesOrder?> GetSalesOrderByIdAsync(
        string soNumber,
        CancellationToken ct = default)
    {
        var so = SeedData.FirstOrDefault(o => o.SoNumber == soNumber);
        return Task.FromResult(so);
    }
}
