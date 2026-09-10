namespace ElectronicStore.Models;

/// <summary>
/// The order state machine (CORE-18). Kept next to <see cref="OrderStatus"/> rather than
/// inside the service so the UI can ask the same questions the service enforces — an admin
/// screen can build its status dropdown from <see cref="AllowedNextStatuses"/> and will
/// never offer a transition the service would reject.
/// </summary>
public static class OrderStatusRules
{
    /// <summary>
    /// Forward flow is strictly step by step: Pending → Confirmed → Preparing → Shipping →
    /// Completed. Cancelling is allowed until the parcel leaves the shop. Completed and
    /// Cancelled are terminal, so no entry lists them as a source of further movement.
    /// </summary>
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Transitions =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
            [OrderStatus.Confirmed] = [OrderStatus.Preparing, OrderStatus.Cancelled],
            [OrderStatus.Preparing] = [OrderStatus.Shipping, OrderStatus.Cancelled],
            [OrderStatus.Shipping] = [OrderStatus.Completed],
            [OrderStatus.Completed] = [],
            [OrderStatus.Cancelled] = []
        };

    /// <summary>Whether <paramref name="current"/> may move to <paramref name="next"/>.</summary>
    public static bool CanTransitionTo(this OrderStatus current, OrderStatus next) =>
        Transitions.TryGetValue(current, out var allowed) && Array.IndexOf(allowed, next) >= 0;

    /// <summary>Every status reachable from <paramref name="current"/>; empty when final.</summary>
    public static IReadOnlyList<OrderStatus> AllowedNextStatuses(this OrderStatus current) =>
        Transitions.TryGetValue(current, out var allowed) ? allowed : [];

    /// <summary>Completed and Cancelled never move again.</summary>
    public static bool IsFinal(this OrderStatus status) =>
        status is OrderStatus.Completed or OrderStatus.Cancelled;

    /// <summary>
    /// A customer may only pull back an order the shop has not acted on yet. Once it is
    /// Confirmed, cancelling becomes a shop decision.
    /// </summary>
    public static bool CanBeCancelledByCustomer(this OrderStatus status) =>
        status == OrderStatus.Pending;

    /// <summary>The shop may cancel any order that has not shipped or finished.</summary>
    public static bool CanBeCancelledByAdmin(this OrderStatus status) =>
        status.CanTransitionTo(OrderStatus.Cancelled);
}
