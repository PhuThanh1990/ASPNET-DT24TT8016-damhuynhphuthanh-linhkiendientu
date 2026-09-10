using ElectronicStore.Data;
using ElectronicStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Areas.Admin.Services;

/// <summary>
/// Stop-gap implementation of <see cref="IAdminOrderWorkflow"/> used until the Core
/// OrderService (CORE-16 → CORE-18) is merged.
/// </summary>
/// <remarks>
/// Handles forward transitions, which are a single column write and carry no side effects,
/// and deliberately refuses to cancel — see <see cref="CancelAsync"/>. Replace the DI
/// registration in Program.cs with the Core service when it lands; nothing else changes.
/// </remarks>
public sealed class AdminOrderWorkflow : IAdminOrderWorkflow
{
    /// <summary>Shown wherever cancelling is offered, so the blocker is visible in the UI.</summary>
    public const string CancelBlockedMessage =
        "Hủy đơn đang chờ OrderService của Core (CORE-16 → CORE-18). Hủy đơn bắt buộc phải " +
        "cộng trả tồn kho cho từng sản phẩm; nếu Admin tự cộng ở đây thì khi Core merge tồn " +
        "kho sẽ bị cộng hai lần.";

    private readonly ApplicationDbContext _db;

    public AdminOrderWorkflow(ApplicationDbContext db) => _db = db;

    /// <inheritdoc />
    public bool CanCancelOrders => false;

    /// <inheritdoc />
    public async Task<OrderWorkflowResult> MoveForwardAsync(
        int orderId,
        OrderStatus target,
        CancellationToken cancellationToken)
    {
        // Cancelling is not a forward move; route it through CancelAsync so the stock rule
        // cannot be bypassed by posting Cancelled as a target.
        if (target == OrderStatus.Cancelled)
        {
            return OrderWorkflowResult.Fail(CancelBlockedMessage);
        }

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            return OrderWorkflowResult.Fail("Không tìm thấy đơn hàng.");
        }

        // Re-checked against the row that was just read, not against what the page showed:
        // another admin may have advanced the same order in the meantime.
        if (!OrderStatusRules.CanMoveTo(order.Status, target))
        {
            return OrderWorkflowResult.Fail(
                $"Không thể chuyển đơn từ \"{OrderStatusRules.DisplayName(order.Status)}\" sang " +
                $"\"{OrderStatusRules.DisplayName(target)}\". Trang có thể đã cũ, hãy tải lại.");
        }

        order.Status = target;
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return OrderWorkflowResult.Ok();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Always refuses. Cancelling has to add every <c>OrderDetail.Quantity</c> back onto
    /// <c>Product.StockQuantity</c> (docs/database-schema.md § 9.2) and that logic belongs to
    /// the Core OrderService. Writing a second copy here is exactly how stock ends up restored
    /// twice, so this method changes nothing at all until Core provides the real implementation.
    /// </remarks>
    public Task<OrderWorkflowResult> CancelAsync(int orderId, CancellationToken cancellationToken) =>
        Task.FromResult(OrderWorkflowResult.Fail(CancelBlockedMessage));
}
