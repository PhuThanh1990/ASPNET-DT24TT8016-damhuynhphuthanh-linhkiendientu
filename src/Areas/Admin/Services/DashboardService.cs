using ElectronicStore.Areas.Admin.Models;
using ElectronicStore.Data;
using ElectronicStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Areas.Admin.Services;

/// <summary>
/// Read-only reporting for the Admin dashboard (ADM-17 → ADM-19).
/// </summary>
/// <remarks>
/// Strictly a reader: it never writes, never changes an order status and never touches
/// stock. Order business rules stay in the Core <c>IOrderService</c>/<c>OrderStatusRules</c>;
/// this type only counts and sums what those rules have already produced.
///
/// Every figure is aggregated by SQL Server — no table is pulled into memory to be counted
/// in C#, and no query runs inside a loop.
/// </remarks>
public sealed class DashboardService
{
    /// <summary>
    /// The one status that counts as earned revenue.
    /// </summary>
    /// <remarks>
    /// Core models an order's money but has no notion of "revenue", so the reporting rule is
    /// defined here and used by every figure on the dashboard — the KPI card, the chart and
    /// the best-seller table all read this constant, so they can never disagree.
    ///
    /// <c>Completed</c> means the parcel was delivered, which is the first moment the money
    /// is really earned. <c>Cancelled</c> is excluded by construction, and so are orders
    /// still in flight (Pending → Shipping): counting those would book revenue the shop might
    /// still have to give back.
    /// </remarks>
    public const OrderStatus RevenueStatus = OrderStatus.Completed;

    /// <summary>
    /// Vietnam is UTC+7 all year round — the country observes no daylight saving — so a fixed
    /// offset converts the stored UTC timestamps to local days exactly.
    /// </summary>
    /// <remarks>
    /// Timestamps are stored in UTC (docs/database-schema.md § 1). Grouping the chart by the
    /// raw UTC date would push every order placed after 17:00 local into the next day, so the
    /// shift is applied inside the query and SQL Server does the grouping.
    /// </remarks>
    private const int VietnamUtcOffsetHours = 7;

    private readonly ApplicationDbContext _db;

    public DashboardService(ApplicationDbContext db) => _db = db;

    /// <summary>Builds the whole dashboard.</summary>
    /// <param name="days">Width of the revenue window; one of <see cref="RevenueChartViewModel.DayOptions"/>.</param>
    public async Task<DashboardViewModel> BuildAsync(int days, CancellationToken cancellationToken)
    {
        // Anything outside the offered ranges is ignored rather than trusted, so a crafted
        // query string cannot ask for a 100-year window.
        if (!RevenueChartViewModel.DayOptions.Contains(days))
        {
            days = 30;
        }

        return new DashboardViewModel
        {
            Kpi = await GetKpiAsync(cancellationToken),
            Revenue = await GetRevenueAsync(days, cancellationToken),
            BestSellers = await GetBestSellersAsync(5, cancellationToken),
            RecentOrders = await GetRecentOrdersAsync(5, cancellationToken)
        };
    }

    /// <summary>KPI cards (ADM-17).</summary>
    private async Task<DashboardKpiViewModel> GetKpiAsync(CancellationToken cancellationToken)
    {
        // One GROUP BY gives every order figure at once: the totals per status and the money
        // per status. Six separate COUNT queries would read the same table six times.
        var orderStats = await _db.Orders
            .AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count(),
                Total = g.Sum(o => o.TotalAmount)
            })
            .ToListAsync(cancellationToken);

        // One row per status at most, so finishing the arithmetic in memory is free.
        var kpi = new DashboardKpiViewModel
        {
            TotalOrders = orderStats.Sum(s => s.Count),
            PendingOrders = orderStats.FirstOrDefault(s => s.Status == OrderStatus.Pending)?.Count ?? 0,
            CompletedOrders = orderStats.FirstOrDefault(s => s.Status == OrderStatus.Completed)?.Count ?? 0,
            CancelledOrders = orderStats.FirstOrDefault(s => s.Status == OrderStatus.Cancelled)?.Count ?? 0,
            TotalRevenue = orderStats.FirstOrDefault(s => s.Status == RevenueStatus)?.Total ?? 0m
        };

        // Three product figures in one pass over the table.
        var productStats = await _db.Products
            .AsNoTracking()
            .GroupBy(p => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Active = g.Count(p => p.IsActive),
                OutOfStock = g.Count(p => p.IsActive && p.StockQuantity == 0)
            })
            .FirstOrDefaultAsync(cancellationToken);

        kpi.TotalProducts = productStats?.Total ?? 0;
        kpi.ActiveProducts = productStats?.Active ?? 0;
        kpi.OutOfStockProducts = productStats?.OutOfStock ?? 0;

        // Customers are counted through the Identity join table rather than "every user minus
        // the admins", so an account with no role at all is not miscounted as a customer.
        kpi.TotalCustomers = await (
            from userRole in _db.UserRoles
            join role in _db.Roles on userRole.RoleId equals role.Id
            where role.Name == AppRoles.Customer
            select userRole.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        return kpi;
    }

    /// <summary>Revenue per day over the last <paramref name="days"/> days (ADM-18).</summary>
    private async Task<RevenueChartViewModel> GetRevenueAsync(int days, CancellationToken cancellationToken)
    {
        // The window is expressed in Vietnamese days, then translated back to a UTC lower
        // bound so the WHERE clause can still use the index on Orders.
        var todayLocal = DateTime.UtcNow.AddHours(VietnamUtcOffsetHours).Date;
        var firstDayLocal = todayLocal.AddDays(-(days - 1));
        var fromUtc = firstDayLocal.AddHours(-VietnamUtcOffsetHours);

        var buckets = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Status == RevenueStatus && o.CreatedAt >= fromUtc)
            // Shifting before grouping lets SQL Server do DATEADD + CAST AS date and return
            // at most one row per day, instead of streaming every order back to be bucketed.
            .GroupBy(o => o.CreatedAt.AddHours(VietnamUtcOffsetHours).Date)
            .Select(g => new { Day = g.Key, Total = g.Sum(o => o.TotalAmount) })
            .ToListAsync(cancellationToken);

        var totalsByDay = buckets.ToDictionary(b => b.Day, b => b.Total);

        // Every day in the window gets a point, including the ones with no orders: a gap in
        // the array would make Chart.js draw a broken line, and a null would render as NaN.
        var labels = new List<string>(days);
        var values = new List<decimal>(days);

        for (var offset = 0; offset < days; offset++)
        {
            var day = firstDayLocal.AddDays(offset);
            labels.Add(day.ToString("dd/MM"));
            values.Add(totalsByDay.TryGetValue(day, out var total) ? total : 0m);
        }

        return new RevenueChartViewModel
        {
            Labels = labels,
            Values = values,
            Days = days,
            RevenueStatus = RevenueStatus
        };
    }

    /// <summary>Best selling products, by units sold on revenue-bearing orders (ADM-19).</summary>
    private async Task<IReadOnlyList<BestSellerViewModel>> GetBestSellersAsync(
        int take,
        CancellationToken cancellationToken)
    {
        // Grouped by ProductId, not by the snapshot name: a product that was renamed between
        // two orders must stay one row rather than splitting into two.
        var totals = await _db.OrderDetails
            .AsNoTracking()
            .Where(d => d.Order.Status == RevenueStatus)
            .GroupBy(d => d.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                QuantitySold = g.Sum(d => d.Quantity),
                Revenue = g.Sum(d => d.LineTotal)
            })
            .OrderByDescending(x => x.QuantitySold)
            .ThenByDescending(x => x.Revenue)
            .Take(take)
            .ToListAsync(cancellationToken);

        if (totals.Count == 0)
        {
            return [];
        }

        // A second, bounded query for the display columns — at most `take` ids, so this is one
        // extra round trip rather than one per row.
        var productIds = totals.Select(t => t.ProductId).ToList();

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Name,
                ImageUrl = p.ProductImages
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.SortOrder)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault()
            })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return totals
            .Select(t =>
            {
                products.TryGetValue(t.ProductId, out var product);

                return new BestSellerViewModel
                {
                    ProductId = t.ProductId,
                    // OrderDetail.ProductId is Restrict, so a sold product cannot be deleted
                    // and the lookup should always hit; the fallback only guards a bad row.
                    ProductName = product?.Name ?? $"#{t.ProductId}",
                    ImageUrl = product?.ImageUrl,
                    QuantitySold = t.QuantitySold,
                    Revenue = t.Revenue
                };
            })
            .ToList();
    }

    /// <summary>The newest orders, whatever their status (ADM-19).</summary>
    private async Task<IReadOnlyList<RecentOrderViewModel>> GetRecentOrdersAsync(
        int take,
        CancellationToken cancellationToken) =>
        await _db.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Take(take)
            .Select(o => new RecentOrderViewModel
            {
                Id = o.Id,
                OrderCode = o.OrderCode,
                CustomerName = o.User.FullName,
                CreatedAt = o.CreatedAt,
                TotalAmount = o.TotalAmount,
                Status = o.Status
            })
            .ToListAsync(cancellationToken);
}
