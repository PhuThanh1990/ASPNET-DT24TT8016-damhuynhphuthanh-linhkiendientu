using ElectronicStore.Areas.Admin.Models;
using ElectronicStore.Areas.Admin.Services;
using ElectronicStore.Data;
using ElectronicStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Areas.Admin.Controllers;

/// <summary>
/// Order administration: list (ADM-14), detail (ADM-15) and status changes (ADM-16).
/// </summary>
/// <remarks>
/// Reading is done straight from the DbContext; changing a status always goes through
/// <see cref="IAdminOrderWorkflow"/> so the transition rules and the stock consequences stay
/// in one place and can be handed to the Core OrderService without touching this controller.
/// </remarks>
public class OrderController : AdminControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAdminOrderWorkflow _workflow;

    public OrderController(ApplicationDbContext db, IAdminOrderWorkflow workflow)
    {
        _db = db;
        _workflow = workflow;
    }

    // GET /Admin/Order?status=Pending
    public async Task<IActionResult> Index(OrderStatus? status, CancellationToken cancellationToken)
    {
        // Projected rather than Include(User).Include(OrderDetails): the list needs two user
        // columns and a line count, so this stays one query with a join and a sub-select
        // instead of materialising every order line. Matches IX (Status, CreatedAt DESC).
        var query = _db.Orders.AsNoTracking();

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

        // The service re-reads the order and re-checks the transition, so a stale page or a
        // hand-crafted POST cannot skip a step.
        var result = await _workflow.MoveForwardAsync(id, target, cancellationToken);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            TempData["SuccessMessage"] =
                $"Đã chuyển đơn sang trạng thái \"{OrderStatusRules.DisplayName(target)}\".";
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

        // Cancelling has to return the reserved stock. That logic belongs to the Core
        // OrderService, so this controller only forwards the request — it never touches
        // Product.StockQuantity itself.
        var result = await _workflow.CancelAsync(id, cancellationToken);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            TempData["SuccessMessage"] = "Đã hủy đơn hàng và hoàn trả tồn kho.";
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

        model.NextStatus = OrderStatusRules.NextStatus(model.Status);
        model.CancelAllowedByWorkflow = OrderStatusRules.CanCancel(model.Status);
        model.CancelSupported = _workflow.CanCancelOrders;
        model.CancelBlockedReason = _workflow.CanCancelOrders ? null : AdminOrderWorkflow.CancelBlockedMessage;

        return model;
    }
}
