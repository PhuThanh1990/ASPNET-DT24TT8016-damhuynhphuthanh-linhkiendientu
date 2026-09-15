using ElectronicStore.Areas.Admin.Models;
using ElectronicStore.Data;
using ElectronicStore.Models;
using ElectronicStore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Areas.Admin.Controllers;

/// <summary>
/// Order administration: list (ADM-14), detail (ADM-15) and status changes (ADM-16).
/// </summary>
/// <remarks>
/// Reading is done straight from the DbContext with projections shaped for these screens —
/// that is presentation, not business logic. Every write goes through the Core
/// <see cref="IOrderService"/> (CORE-16 → CORE-18), which owns the transition table, the
/// stock restore and the concurrency handling. This controller holds no order rules of its
/// own and never touches <c>Product.StockQuantity</c>.
/// </remarks>
public class OrderController : AdminControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IOrderService _orders;

    public OrderController(ApplicationDbContext db, IOrderService orders)
    {
        _db = db;
        _orders = orders;
    }

    // GET /Admin/Order?status=Pending
    public async Task<IActionResult> Index(OrderStatus? status, CancellationToken cancellationToken)
    {
        // Chỉ nhận giá trị thật sự có trong enum OrderStatus của Core. "?status=abcxyz" đã bị
        // model binding bỏ qua (status = null), nhưng "?status=99" thì bind lọt thành
        // (OrderStatus)99 và sẽ lọc ra 0 dòng kèm thanh nút không nút nào sáng — khó hiểu hơn
        // là chỉ hiện tất cả. Giá trị lạ vì vậy được coi như không lọc.
        if (status is { } requested && !Enum.IsDefined(requested))
        {
            status = null;
        }

        // Projected rather than Include(User).Include(OrderDetails): the list needs two user
        // columns and a line count, so this stays one query with a join and a sub-select
        // instead of materialising every order line. Matches IX (Status, CreatedAt DESC).
        var query = _db.Orders.AsNoTracking();

        // Lọc trên IQueryable trước khi sắp xếp/chiếu nên WHERE chạy dưới SQL Server, không
        // nạp toàn bộ đơn lên rồi lọc trong bộ nhớ.
        if (status is { } selected)
        {
            query = query.Where(o => o.Status == selected);
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderListItemViewModel
            {
                Id = o.Id,
                OrderCode = o.OrderCode,
                CustomerName = o.User.FullName,
                CustomerEmail = o.User.Email,
                ShippingFullName = o.ShippingFullName,
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                ItemCount = o.OrderDetails.Count()
            })
            .ToListAsync(cancellationToken);

        // One grouped query for every badge count, instead of one COUNT per status.
        // Cố ý đếm trên toàn bộ bảng (không áp dụng filter) để badge luôn cho biết mỗi trạng
        // thái đang có bao nhiêu đơn, kể cả khi đang đứng ở một trạng thái khác.
        var counts = await _db.Orders
            .AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var model = new OrderListViewModel
        {
            Orders = orders,
            Status = status,
            CountsByStatus = counts.ToDictionary(c => c.Status, c => c.Count),
            TotalCount = counts.Sum(c => c.Count)
        };

        return View(model);
    }

    // GET /Admin/Order/Details/5
    public async Task<IActionResult> Details(int? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return NotFound();
        }

        var model = await BuildDetailViewModelAsync(id.Value, cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    // POST /Admin/Order/Advance/5 — Confirm / Prepare / Ship / Complete (ADM-16).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Advance(
        [FromRoute] int id,
        OrderStatus target,
        CancellationToken cancellationToken)
    {
        // A completely unknown id is a bad URL, not a business refusal: answer 404 instead of
        // redirecting to a detail page that does not exist and stranding the message there.
        if (!await _db.Orders.AnyAsync(o => o.Id == id, cancellationToken))
        {
            return NotFound();
        }

        // The Core service re-reads the order and re-validates the transition against the
        // status still stored in the database, so a stale page or a hand-crafted POST cannot
        // skip a step. Posting Cancelled here is refused by the service as well, because
        // cancelling has to restore stock — that is what the Cancel action below is for.
        var result = await _orders.UpdateStatusAsync(id, target, cancellationToken);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] =
                $"Đã chuyển đơn sang trạng thái \"{OrderStatusDisplay.DisplayName(target)}\".";
        }
        else
        {
            TempData["ErrorMessage"] = result.FirstError;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /Admin/Order/Cancel/5 (ADM-16)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel([FromRoute] int id, CancellationToken cancellationToken)
    {
        if (!await _db.Orders.AnyAsync(o => o.Id == id, cancellationToken))
        {
            return NotFound();
        }

        // Cancelling returns the reserved stock. The Core service does that inside the same
        // transaction as the status change and only for the caller that actually moved the
        // order to Cancelled, so a double submit cannot restore stock twice.
        var result = await _orders.CancelByAdminAsync(id, cancellationToken);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Đã hủy đơn hàng và hoàn trả tồn kho.";
        }
        else
        {
            TempData["ErrorMessage"] = result.FirstError;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<OrderDetailViewModel?> BuildDetailViewModelAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var model = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new OrderDetailViewModel
            {
                Id = o.Id,
                OrderCode = o.OrderCode,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,

                CustomerName = o.User.FullName,
                CustomerEmail = o.User.Email,
                CustomerPhone = o.User.PhoneNumber,

                ShippingFullName = o.ShippingFullName,
                ShippingPhone = o.ShippingPhone,
                ShippingAddress = o.ShippingAddress,
                Note = o.Note,

                SubTotal = o.SubTotal,
                ShippingFee = o.ShippingFee,
                TotalAmount = o.TotalAmount,

                // Snapshot columns only. The current Product row is never read for display —
                // renaming or repricing a product must not rewrite an existing invoice.
                Lines = o.OrderDetails
                    .OrderBy(d => d.Id)
                    .Select(d => new OrderLineViewModel
                    {
                        ProductId = d.ProductId,
                        ProductStillExists = d.Product != null,
                        ProductName = d.ProductName,
                        UnitPrice = d.UnitPrice,
                        Quantity = d.Quantity,
                        LineTotal = d.LineTotal
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (model is null)
        {
            return null;
        }

        // Both flags come from the Core state machine, so the buttons this screen renders are
        // exactly the moves OrderService will accept. Cancelling is offered separately
        // because it runs through a different service method.
        model.NextStatus = model.Status
            .AllowedNextStatuses()
            .Cast<OrderStatus?>()
            .FirstOrDefault(next => next != OrderStatus.Cancelled);

        model.CanCancel = model.Status.CanBeCancelledByAdmin();

        return model;
    }
}
