using ElectronicStore.Models;

namespace ElectronicStore.Areas.Admin.Services;

/// <summary>Outcome of a status change, so the controller never has to catch exceptions for
/// ordinary business refusals.</summary>
/// <param name="Succeeded">False when the move was refused; <paramref name="Error"/> says why.</param>
/// <param name="Error">Message safe to show to the admin.</param>
public readonly record struct OrderWorkflowResult(bool Succeeded, string? Error)
{
    public static OrderWorkflowResult Ok() => new(true, null);

    public static OrderWorkflowResult Fail(string error) => new(false, error);
}

/// <summary>
/// The seam between the Admin area and order business logic (ADM-16).
/// </summary>
/// <remarks>
/// The Core OrderService (CORE-16 → CORE-18) is not in this source tree yet. The Admin
/// controller talks only to this interface, so when that service merges the integration is
/// a DI registration in Program.cs — no controller or view has to change.
///
/// The split is deliberate:
/// <list type="bullet">
///   <item>forward moves (Pending → … → Completed) only write <c>Order.Status</c>; there is
///   no stock, money or side effect involved, so the default implementation performs them.</item>
///   <item>cancelling must add every line's quantity back to <c>Product.StockQuantity</c>
///   (docs/database-schema.md § 9.2). That belongs to the Core service. Implementing it here
///   as well would restore the stock twice once Core merges, so the default implementation
///   refuses instead.</item>
/// </list>
/// </remarks>
public interface IAdminOrderWorkflow
{
    /// <summary>Moves an order one step forward along the lifecycle.</summary>
    Task<OrderWorkflowResult> MoveForwardAsync(int orderId, OrderStatus target, CancellationToken cancellationToken);

    /// <summary>Cancels an order and returns its reserved stock.</summary>
    Task<OrderWorkflowResult> CancelAsync(int orderId, CancellationToken cancellationToken);

    /// <summary>
    /// False while cancelling is not backed by real stock-restore logic, so the UI can show
    /// the button disabled with an explanation instead of pretending it works.
    /// </summary>
    bool CanCancelOrders { get; }
}
