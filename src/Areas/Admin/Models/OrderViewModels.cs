using ElectronicStore.Models;

namespace ElectronicStore.Areas.Admin.Models;

/// <summary>One row of the admin order list (ADM-14).</summary>
public class OrderListItemViewModel
{
    public int Id { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    /// <summary>Account holder's name; falls back to the e-mail when the profile is empty.</summary>
    public string CustomerName { get; set; } = string.Empty;

    public string? CustomerEmail { get; set; }

    /// <summary>Receiver as snapshotted at checkout — may differ from the account holder.</summary>
    public string ShippingFullName { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public OrderStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public int ItemCount { get; set; }
}

/// <summary>The order list plus its status filter (ADM-14).</summary>
public class OrderListViewModel
{
    public IReadOnlyList<OrderListItemViewModel> Orders { get; set; } = [];

    /// <summary>Currently selected status, or null for "all".</summary>
    public OrderStatus? Status { get; set; }

    /// <summary>Row counts per status, so the filter shows how much is waiting.</summary>
    public IReadOnlyDictionary<OrderStatus, int> CountsByStatus { get; set; } =
        new Dictionary<OrderStatus, int>();

    public int TotalCount { get; set; }
}

/// <summary>
/// One order line on the detail screen (ADM-15). Every value is the snapshot written at
/// checkout — never the product's current name or price (docs/database-schema.md § 9.1).
/// </summary>
public class OrderLineViewModel
{
    /// <summary>Only used to link back to the product page; never displayed as data.</summary>
    public int ProductId { get; set; }

    /// <summary>True when the product row still exists, so the link can be hidden if not.</summary>
    public bool ProductStillExists { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal { get; set; }
}

/// <summary>The admin order detail screen (ADM-15) and its available actions (ADM-16).</summary>
public class OrderDetailViewModel
{
    public int Id { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public OrderStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    // Customer account.
    public string CustomerName { get; set; } = string.Empty;

    public string? CustomerEmail { get; set; }

    public string? CustomerPhone { get; set; }

    // Shipping snapshot.
    public string ShippingFullName { get; set; } = string.Empty;

    public string ShippingPhone { get; set; } = string.Empty;

    public string ShippingAddress { get; set; } = string.Empty;

    public string? Note { get; set; }

    // Money.
    public decimal SubTotal { get; set; }

    public decimal ShippingFee { get; set; }

    public decimal TotalAmount { get; set; }

    public IReadOnlyList<OrderLineViewModel> Lines { get; set; } = [];

    /// <summary>
    /// The next step forward, or null when the order is finished or cancelled. Filled from
    /// the Core state machine (<see cref="OrderStatusRules.AllowedNextStatuses"/>), so the
    /// button can never offer a move the service would reject.
    /// </summary>
    public OrderStatus? NextStatus { get; set; }

    /// <summary>
    /// True when the shop may still cancel this order, per
    /// <see cref="OrderStatusRules.CanBeCancelledByAdmin"/>.
    /// </summary>
    public bool CanCancel { get; set; }
}
