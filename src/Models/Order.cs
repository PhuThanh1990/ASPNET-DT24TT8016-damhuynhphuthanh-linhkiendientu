namespace ElectronicStore.Models;

/// <summary>
/// A customer order. Shipping details and money are snapshotted at checkout so that later
/// edits to the address book or to product prices never rewrite an existing order —
/// see docs/database-schema.md § 9.1.
/// Mapping lives in <c>Data/Configurations/OrderConfiguration.cs</c>.
/// </summary>
public class Order
{
    public int Id { get; set; }

    /// <summary>Human readable code shown to the customer, e.g. "DH20260910-0007".</summary>
    public string OrderCode { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The address book entry the order was placed from, kept for traceability only.
    /// Becomes null if the customer later deletes that address; the snapshot below stays.
    /// </summary>
    public int? AddressId { get; set; }

    public string ShippingFullName { get; set; } = string.Empty;

    public string ShippingPhone { get; set; } = string.Empty;

    /// <summary>Full address flattened into one line at checkout.</summary>
    public string ShippingAddress { get; set; } = string.Empty;

    /// <summary>Sum of every line total.</summary>
    public decimal SubTotal { get; set; }

    public decimal ShippingFee { get; set; }

    /// <summary>What the customer pays: <see cref="SubTotal"/> + <see cref="ShippingFee"/>.</summary>
    public decimal TotalAmount { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    /// <summary>Free text note from the customer.</summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Address? Address { get; set; }

    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
