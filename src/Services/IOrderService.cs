using ElectronicStore.Models;

namespace ElectronicStore.Services;

/// <summary>
/// All order business rules: placing an order, reading orders back and moving them through
/// their lifecycle. Controllers call these methods and translate the result into a view —
/// no pricing, stock or status logic belongs in a controller.
/// </summary>
/// <remarks>
/// Read and write methods come in a customer flavour and an admin flavour. The customer
/// ones take the signed-in user id and refuse to touch anybody else's order, so ownership
/// cannot be forgotten at the call site.
/// </remarks>
public interface IOrderService
{
    /// <summary>
    /// Validates the cart against live catalog data, snapshots prices and shipping details,
    /// decrements stock and stores the order — all inside one transaction.
    /// </summary>
    Task<OrderResult<Order>> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>One order with its lines, only if it belongs to <paramref name="userId"/>.</summary>
    Task<OrderResult<Order>> GetForCustomerAsync(int orderId, string userId, CancellationToken cancellationToken = default);

    /// <summary>One order with its lines and the buyer, for the admin screens.</summary>
    Task<OrderResult<Order>> GetForAdminAsync(int orderId, CancellationToken cancellationToken = default);

    /// <summary>Order history of one customer, newest first.</summary>
    Task<PagedResult<Order>> GetCustomerOrdersAsync(string userId, OrderQuery query, CancellationToken cancellationToken = default);

    /// <summary>Every order, optionally filtered by status or keyword.</summary>
    Task<PagedResult<Order>> GetAdminOrdersAsync(OrderQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// The shipping fee <see cref="PlaceOrderAsync"/> would charge for this subtotal.
    /// </summary>
    /// <remarks>
    /// A read-only quote, added so the checkout page can show the customer the same total the
    /// order will end up carrying instead of repeating the fee rule in the UI. It changes
    /// nothing: the authoritative number is still the one computed while placing the order.
    /// Same idea as <see cref="OrderStatusRules"/> — the UI asks the owner of the rule.
    /// </remarks>
    decimal QuoteShippingFee(decimal subTotal);

    /// <summary>
    /// Moves an order one step along the lifecycle. Rejects any transition the state machine
    /// does not allow. Cancelling is not accepted here because it has to restore stock —
    /// use <see cref="CancelByAdminAsync"/>.
    /// </summary>
    Task<OrderResult<Order>> UpdateStatusAsync(int orderId, OrderStatus newStatus, CancellationToken cancellationToken = default);

    /// <summary>Customer pulls back their own order. Only while it is still Pending.</summary>
    Task<OrderResult<Order>> CancelByCustomerAsync(int orderId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Shop cancels an order that has not shipped yet.</summary>
    Task<OrderResult<Order>> CancelByAdminAsync(int orderId, CancellationToken cancellationToken = default);
}
