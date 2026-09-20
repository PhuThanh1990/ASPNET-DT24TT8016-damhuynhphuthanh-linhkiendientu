namespace ElectronicStore.Models;

/// <summary>
/// Lifecycle of an order. Stored as int; see docs/database-schema.md § 9.2 for the
/// allowed transitions (Completed and Cancelled are terminal).
/// </summary>
public enum OrderStatus
{
    /// <summary>Placed by the customer, waiting for the shop to confirm.</summary>
    Pending = 0,

    /// <summary>Confirmed by the shop.</summary>
    Confirmed = 1,

    /// <summary>Being packed.</summary>
    Preparing = 2,

    /// <summary>Handed over to the carrier.</summary>
    Shipping = 3,

    /// <summary>Delivered successfully.</summary>
    Completed = 4,

    /// <summary>Cancelled by the customer or by the shop.</summary>
    Cancelled = 5
}
