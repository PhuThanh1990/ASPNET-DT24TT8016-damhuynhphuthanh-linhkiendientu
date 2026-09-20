using ElectronicStore.Models;

namespace ElectronicStore.Areas.Admin.Models;

/// <summary>Everything the Admin dashboard renders (ADM-17 → ADM-19).</summary>
public class DashboardViewModel
{
    public DashboardKpiViewModel Kpi { get; set; } = new();

    public RevenueChartViewModel Revenue { get; set; } = new();

    public IReadOnlyList<BestSellerViewModel> BestSellers { get; set; } = [];

    public IReadOnlyList<RecentOrderViewModel> RecentOrders { get; set; } = [];
}

/// <summary>The KPI cards (ADM-17). Every number comes from an aggregate run in SQL.</summary>
public class DashboardKpiViewModel
{
    public int TotalOrders { get; set; }

    public int PendingOrders { get; set; }

    public int CompletedOrders { get; set; }

    public int CancelledOrders { get; set; }

    /// <summary>Sum of <c>Order.TotalAmount</c> over revenue-bearing orders only.</summary>
    public decimal TotalRevenue { get; set; }

    public int TotalProducts { get; set; }

    /// <summary>Products still on sale (<c>IsActive</c>).</summary>
    public int ActiveProducts { get; set; }

    /// <summary>Products on sale whose stock has run out — the one card that needs action.</summary>
    public int OutOfStockProducts { get; set; }

    /// <summary>Accounts holding the <c>Customer</c> role.</summary>
    public int TotalCustomers { get; set; }
}

/// <summary>
/// Revenue per day for the selected window (ADM-18), already shaped for Chart.js:
/// <see cref="Labels"/> and <see cref="Values"/> are the same length and never null.
/// </summary>
public class RevenueChartViewModel
{
    /// <summary>Day labels in Vietnamese order, e.g. "05/09".</summary>
    public IReadOnlyList<string> Labels { get; set; } = [];

    /// <summary>Revenue per label. Days without orders are 0, never null.</summary>
    public IReadOnlyList<decimal> Values { get; set; } = [];

    /// <summary>Window length in days, as picked by the admin.</summary>
    public int Days { get; set; }

    /// <summary>
    /// Order status that counts as revenue, set by the service so the empty state can name
    /// the rule without the view having to reach into the service layer.
    /// </summary>
    public OrderStatus RevenueStatus { get; set; } = OrderStatus.Completed;

    /// <summary>Ranges offered by the toolbar.</summary>
    public static IReadOnlyList<int> DayOptions { get; } = [7, 30, 90];

    public decimal Total => Values.Sum();

    /// <summary>True when the whole window is empty, so the view can show a message instead of a flat line.</summary>
    public bool IsEmpty => Values.Count == 0 || Values.All(v => v == 0m);
}

/// <summary>One row of the best-seller table (ADM-19).</summary>
public class BestSellerViewModel
{
    public int ProductId { get; set; }

    /// <summary>Current catalog name, so the row matches what the admin sees elsewhere.</summary>
    public string ProductName { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public int QuantitySold { get; set; }

    public decimal Revenue { get; set; }
}

/// <summary>
/// One row of the recent-orders table (ADM-19). Deliberately narrower than the order list:
/// no e-mail, phone or address — the dashboard is a summary, the detail screen is where
/// personal data belongs.
/// </summary>
public class RecentOrderViewModel
{
    public int Id { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public decimal TotalAmount { get; set; }

    public OrderStatus Status { get; set; }
}
