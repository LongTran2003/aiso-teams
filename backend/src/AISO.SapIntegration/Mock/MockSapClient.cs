using AISO.Domain.Kpi;
using AISO.Domain.SalesOrders;
using AISO.Domain.Approvals;
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
            OwnerSapUser = "DEV-249",
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
            OwnerSapUser = "DEV-024",
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
            OwnerSapUser = "DEV-249",
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
        new()
        {
            SoNumber = "0000005099",
            CustomerId = "1000",
            CustomerName = "Philly Bikes",
            OrderDate = new DateOnly(2026, 6, 1),
            NetValue = 100m,
            Currency = "USD",
            SalesOrg = "UE00",
            Status = SalesOrderStatus.Open,
            HasInvalidMaterial = true,
            Items = new List<SalesOrderItem>
            {
                new()
                {
                    ItemNumber = "00010",
                    Material = "BAD_MAT",
                    Description = "Missing master",
                    Quantity = 1m,
                    Unit = "EA",
                    NetValue = 100m,
                },
            },
        },
    };

    public Task<IReadOnlyList<SalesOrder>> GetSalesOrdersAsync(SalesOrdersQuery query, CancellationToken ct = default)
    {
        _logger?.LogDebug("MockSapClient.GetSalesOrdersAsync: customer={Customer}, salesOrg={SalesOrg}, top={Top}",
            query.CustomerIdOrName, query.SalesOrg, query.Top);

        IEnumerable<SalesOrder> q = SeedData.Select(WithOwnerOverlay);
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
        if (!string.IsNullOrWhiteSpace(query.OwnerSapUser))
        {
            var owner = query.OwnerSapUser.Trim();
            q = q.Where(o => string.Equals(o.OwnerSapUser, owner, StringComparison.OrdinalIgnoreCase));
        }
        if (query.ExcludeInvalidMaterials)
            q = q.Where(o => !o.HasInvalidMaterial);

        var result = q.OrderByDescending(o => o.OrderDate).Take(Math.Clamp(query.Top, 1, 50)).ToList();
        return Task.FromResult<IReadOnlyList<SalesOrder>>(result);
    }

    public Task<IReadOnlyList<SapValidMaterialPlant>> GetValidMaterialPlantsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<SapValidMaterialPlant> list = new List<SapValidMaterialPlant>
        {
            new("000000000000000110", "1010", "ROH", "EA"),
            new("000000000000000111", "1010", "ROH", "EA"),
            new("000000000000000113", "1010", "ROH", "EA")
        };
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<SapValidMaterialSales>> GetValidMaterialSalesAsync(
        string? salesOrg = null,
        string? distChannel = null,
        int top = 30,
        CancellationToken ct = default)
    {
        // CDS v5: (Material, SalesOrg, DistChannel, Plant) — same Material can repeat per plant.
        var allMaterials = new List<SapValidMaterialSales>
        {
            new("000000000000000110", "1000", "10", "1010"),
            new("000000000000000111", "1000", "10", "1010"),
            new("000000000000000113", "1000", "10", "1010"),
            new("000000000000000110", "1000", "00", "1010"),
            new("000000000000000111", "1000", "00", "1010"),
            new("000000000000000113", "1000", "00", "1010"),
            // Same material but in a different valid plant — must not appear twice in dropdown.
            new("000000000000000110", "1000", "10", "1020")
        };

        IEnumerable<SapValidMaterialSales> filtered = allMaterials;
        if (!string.IsNullOrWhiteSpace(salesOrg))
            filtered = filtered.Where(m => string.Equals(m.SalesOrg, salesOrg, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(distChannel))
            filtered = filtered.Where(m => string.Equals(m.DistChannel, distChannel, StringComparison.OrdinalIgnoreCase));

        // Match the SAP client dedupe behaviour: distinct by Material, keep first plant.
        IReadOnlyList<SapValidMaterialSales> list = filtered
            .GroupBy(m => m.Material)
            .Select(g => g.First())
            .ToList();
        return Task.FromResult(list);
    }

    public Task<SalesOrder?> GetSalesOrderByIdAsync(string soNumber, CancellationToken ct = default)
    {
        _logger?.LogDebug("MockSapClient.GetSalesOrderByIdAsync: {SoNumber}", soNumber);
        var order = SeedData.FirstOrDefault(x => x.SoNumber == soNumber);
        if (order is null)
            return Task.FromResult<SalesOrder?>(null);

        return Task.FromResult<SalesOrder?>(WithOwnerOverlay(order));
    }

    private SalesOrder WithOwnerOverlay(SalesOrder order) =>
        _owners.TryGetValue(order.SoNumber, out var owner)
            ? order with { OwnerSapUser = owner }
            : order;

    public Task<SalesOrder> CreateSalesOrderAsync(CreateSalesOrderDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.RequestingSapUser))
            throw new ArgumentException("RequestingSapUser is required for createSalesOrder.", nameof(dto));

        var soNumber = "9999999999";
        _owners[soNumber] = dto.RequestingSapUser.Trim();
        return Task.FromResult(new SalesOrder
        {
            SoNumber = soNumber,
            CustomerId = dto.Customer,
            CustomerName = "Mock Customer",
            SalesOrg = dto.SalesOrg,
            OrderDate = DateOnly.FromDateTime(DateTime.Now),
            NetValue = 1000m,
            Currency = dto.Currency,
            Status = SalesOrderStatus.Open,
            OwnerSapUser = dto.RequestingSapUser.Trim(),
            Items = Array.Empty<SalesOrderItem>(),
        });
    }

    public Task SyncUserRoleAsync(
        string targetSapUser,
        string newRole,
        string? salesOrg,
        string requestingAdminSapUser,
        CancellationToken ct = default)
    {
        _logger?.LogDebug(
            "MockSapClient.SyncUserRoleAsync: target={Target} role={Role} org={Org} by={Admin}",
            targetSapUser, newRole, salesOrg, requestingAdminSapUser);
        return Task.CompletedTask;
    }

    public Task<SalesOrder> UpdateReferenceAsync(string soNumber, string newReference, string requestingSapUser, CancellationToken ct = default)
    {
        var order = SeedData.FirstOrDefault(x => x.SoNumber == soNumber)
            ?? throw new InvalidOperationException($"Order {soNumber} not found.");
        return Task.FromResult(order with { CustomerReference = newReference });
    }

    public Task<SalesOrder> UpdateSalesOrderAsync(UpdateSalesOrderDto dto, CancellationToken ct = default)
    {
        var so = FormatSo(dto.SoNumber);
        var order = SeedData.FirstOrDefault(x => x.SoNumber == so)
            ?? throw new InvalidOperationException($"Order {dto.SoNumber} not found.");

        var items = order.Items.ToList();
        foreach (var op in dto.Items ?? Array.Empty<UpdateSalesOrderItemDto>())
        {
            var flag = (op.Operation ?? string.Empty).Trim().ToUpperInvariant();
            var itemNo = (op.ItemNumber ?? string.Empty).Trim().PadLeft(6, '0');
            if (flag == "D")
            {
                items.RemoveAll(i => string.Equals(i.ItemNumber.PadLeft(6, '0'), itemNo, StringComparison.Ordinal));
            }
            else if (flag == "I")
            {
                items.Add(new SalesOrderItem
                {
                    ItemNumber = itemNo == "000000" ? ((items.Count + 1) * 10).ToString("000000") : itemNo,
                    Material = op.Material ?? "TG11",
                    Description = op.Material ?? "TG11",
                    Quantity = op.OrderQty ?? 1m,
                    Unit = op.Unit ?? "PC",
                    NetValue = 0m
                });
            }
            else if (flag == "U")
            {
                var idx = items.FindIndex(i => string.Equals(i.ItemNumber.PadLeft(6, '0'), itemNo, StringComparison.Ordinal));
                if (idx >= 0)
                {
                    var cur = items[idx];
                    items[idx] = cur with
                    {
                        Material = string.IsNullOrWhiteSpace(op.Material) ? cur.Material : op.Material!,
                        Quantity = op.OrderQty ?? cur.Quantity,
                        Unit = string.IsNullOrWhiteSpace(op.Unit) ? cur.Unit : op.Unit!
                    };
                }
            }
        }

        DateOnly? reqDate = order.RequestedDeliveryDate;
        if (!string.IsNullOrWhiteSpace(dto.ReqDeliveryDate) && DateOnly.TryParse(dto.ReqDeliveryDate, out var parsed))
            reqDate = parsed;

        return Task.FromResult(order with
        {
            CustomerReference = string.IsNullOrWhiteSpace(dto.PurchaseOrderRef)
                ? order.CustomerReference
                : dto.PurchaseOrderRef,
            RequestedDeliveryDate = reqDate,
            Items = items
        });
    }

    private static string FormatSo(string soNumber) =>
        string.IsNullOrWhiteSpace(soNumber) ? soNumber : soNumber.Trim().PadLeft(10, '0');

    public Task<SalesOrder> RejectOrderAsync(string soNumber, string rejectionCode, string requestingTeamsUser, CancellationToken ct = default)
    {
        var order = SeedData.FirstOrDefault(x => x.SoNumber == soNumber)
            ?? throw new InvalidOperationException($"Order {soNumber} not found.");
        return Task.FromResult(order with { Status = SalesOrderStatus.Cancelled });
    }

    public Task<SalesOrder> CancelOrderAsync(
        string soNumber,
        string requestingSapUser,
        string? reason = null,
        CancellationToken ct = default)
        => RejectOrderAsync(soNumber, "Z1", requestingSapUser, ct);

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

    /// <summary>
    /// Returns a mock SAP user-role row for known demo SAP users (DEV-*).
    /// Unknown SAP users yield <c>null</c>, matching the real
    /// <c>SapClient.GetUserRoleAsync</c> contract.
    /// </summary>
    public Task<SapUserRoleRow?> GetUserRoleAsync(string sapUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sapUserId))
            return Task.FromResult<SapUserRoleRow?>(null);

        var normalized = sapUserId.Trim().ToUpperInvariant();
        SapUserRoleRow? row = normalized switch
        {
            // Demo seed: Employee + TV01
            "DEV-249" => new SapUserRoleRow(normalized, "EMPLOYEE", "TV01"),
            // Demo seed: Manager + UE00
            "DEV-024" => new SapUserRoleRow(normalized, "MANAGER", "UE00"),
            // Quân: Admin + DN00 (smoke test for Admin)
            "DEV-230" => new SapUserRoleRow(normalized, "ADMIN", "DN00"),
            // Generic DEV-* users get a default Employee + TV01 row.
            _ when normalized.StartsWith("DEV-", StringComparison.Ordinal)
                => new SapUserRoleRow(normalized, "EMPLOYEE", "TV01"),
            _ => null,
        };
        return Task.FromResult(row);
    }

    public Task<IReadOnlyList<SapSalesArea>> GetSalesAreasAsync(string? salesOrg = null, CancellationToken ct = default)
    {
        IReadOnlyList<SapSalesArea> allAreas =
        [
            new("TV01", "10", "00"),
            new("FU24", "10", "00"),
            new("UE00", "10", "00"),
            new("UW00", "10", "00"),
            new("DN00", "10", "00"),
            new("DS00", "10", "00")
        ];

        if (string.IsNullOrWhiteSpace(salesOrg))
            return Task.FromResult(allAreas);

        var filtered = allAreas.Where(a => string.Equals(a.SalesOrg, salesOrg, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult<IReadOnlyList<SapSalesArea>>(filtered);
    }

    public Task<IReadOnlyList<SapMaterial>> GetMaterialsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<SapMaterial> materials =
        [
            new("TG11", "Trading Goods 11", DateTimeOffset.UtcNow),
            new("TG12", "Trading Goods 12", DateTimeOffset.UtcNow),
            new("TG13", "Trading Goods 13", DateTimeOffset.UtcNow),
            new("DXTR1000", "Deluxe Touring Bike (Black)", DateTimeOffset.UtcNow),
            new("WDFR1000", "Water Bottle (Front)", DateTimeOffset.UtcNow)
        ];
        return Task.FromResult(materials);
    }

    public Task<IReadOnlyList<SapValidCustomer>> GetValidCustomersAsync(
        string? salesOrg = null,
        string? distChannel = null,
        string? division = null,
        int top = 30,
        CancellationToken ct = default)
    {
        IEnumerable<SapValidCustomer> rows =
        [
            new("10100001", "TV01", "10", "00", "Domestic Customer US"),
            new("10100002", "TV01", "10", "00", "Philly Bikes"),
            new("10100001", "FU24", "10", "00", "Domestic Customer US"),
            new("17100001", "FU24", "10", "00", "Customer FU24"),
            new("100323", "FU24", "FR", "FG", "Customer FR/FG")
        ];

        if (!string.IsNullOrWhiteSpace(salesOrg))
            rows = rows.Where(r => string.Equals(r.SalesOrg, salesOrg.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(distChannel))
            rows = rows.Where(r => string.Equals(r.DistChannel, distChannel.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(division))
            rows = rows.Where(r => string.Equals(r.Division, division.Trim(), StringComparison.OrdinalIgnoreCase));

        return Task.FromResult<IReadOnlyList<SapValidCustomer>>(rows.Take(Math.Clamp(top, 1, 200)).ToList());
    }

    public async Task<bool?> IsCustomerValidForSalesAreaAsync(
        string customer,
        string salesOrg,
        string distChannel,
        string division,
        CancellationToken ct = default)
    {
        var raw = customer.Trim();
        var stripped = raw.TrimStart('0');
        if (string.IsNullOrEmpty(stripped))
            stripped = raw;

        var rows = await GetValidCustomersAsync(salesOrg, distChannel, division, top: 200, ct);
        return rows.Any(r =>
        {
            var id = r.Customer.Trim().TrimStart('0');
            if (string.IsNullOrEmpty(id))
                id = r.Customer.Trim();
            return string.Equals(id, stripped, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.Customer.Trim(), raw, StringComparison.OrdinalIgnoreCase);
        });
    }
    public Task DelegateApprovalAsync(DelegateApprovalDto dto, CancellationToken ct = default)
    {
        _logger?.LogInformation(
            "MockSapClient: DelegateApprovalAsync called by {RequestingUser} to delegate {DelegateUser}",
            dto.RequestingTeamsUser, dto.DelegateUser);
        return Task.CompletedTask;
    }

    public Task RevokeDelegationAsync(RevokeDelegationDto dto, CancellationToken ct = default)
    {
        _logger?.LogInformation(
            "MockSapClient: RevokeDelegationAsync called by {RequestingUser} to revoke {DelegateUser}",
            dto.RequestingTeamsUser, dto.DelegateUser);
        return Task.CompletedTask;
    }
}
