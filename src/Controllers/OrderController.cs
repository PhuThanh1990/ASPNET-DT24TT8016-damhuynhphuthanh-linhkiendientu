using ElectronicStore.Data;
using ElectronicStore.Models;
using ElectronicStore.Models.ViewModels;
using ElectronicStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Controllers;

/// <summary>
/// CUS-19 — "Đơn hàng của tôi" và chi tiết đơn hàng của khách đang đăng nhập.
/// </summary>
/// <remarks>
/// Chống IDOR: không action nào nhận UserId từ URL hay từ form. Id người dùng lấy từ cookie
/// đăng nhập rồi truyền vào các method "ForCustomer" của Core <see cref="IOrderService"/> —
/// những method này tự từ chối đơn của tài khoản khác, nên không thể quên kiểm tra quyền sở
/// hữu ở chỗ gọi. Hủy đơn cũng gọi service (Core hoàn lại tồn kho), controller không tự
/// sửa trạng thái hay cộng trả kho.
/// </remarks>
[Authorize]
public class OrderController : Controller
{
    private const int PageSize = 10;

    private readonly IOrderService _orders;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrderController(
        IOrderService orders,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        _orders = orders;
        _db = db;
        _userManager = userManager;
    }

    /// <summary>Lịch sử đơn hàng, mới nhất trước.</summary>
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var result = await _orders.GetCustomerOrdersAsync(
            userId,
            new OrderQuery { Page = Math.Max(page, 1), PageSize = PageSize },
            cancellationToken);

        var itemCounts = await CountLinesAsync(result.Items, cancellationToken);

        var model = new CustomerOrderListViewModel
        {
            CurrentPage = result.Page,
            PageSize = result.PageSize,
            TotalItems = result.TotalCount,
            TotalPages = result.TotalPages,
            Orders = result.Items
                .Select(order => new CustomerOrderSummaryViewModel
                {
                    Id = order.Id,
                    OrderCode = order.OrderCode,
                    CreatedAt = order.CreatedAt,
                    TotalAmount = order.TotalAmount,
                    Status = order.Status,
                    ItemCount = itemCounts.GetValueOrDefault(order.Id),
                })
                .ToList(),
        };

        return View(model);
    }

    /// <summary>
    /// Chi tiết một đơn. Đơn của người khác trả về 404 giống hệt đơn không tồn tại, để không
    /// thể dò xem id nào đang tồn tại.
    /// </summary>
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var result = await _orders.GetForCustomerAsync(id, userId, cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound();
        }

        return View(CustomerOrderDetailViewModel.FromOrder(result.Value!));
    }

    /// <summary>
    /// Khách tự hủy đơn. Core quyết định đơn có được hủy hay không
    /// (<see cref="OrderStatusRules.CanBeCancelledByCustomer"/>) và hoàn lại tồn kho.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var result = await _orders.CancelByCustomerAsync(id, userId, cancellationToken);

        if (result.Succeeded)
        {
            TempData["StatusMessageType"] = "success";
            TempData["StatusMessage"] = "Đơn hàng đã được hủy.";

            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.ErrorCode == OrderErrorCode.NotFound)
        {
            return NotFound();
        }

        TempData["StatusMessageType"] = "danger";
        TempData["StatusMessage"] = result.FirstError;

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Số dòng hàng của từng đơn trong trang hiện tại, gộp thành một câu GROUP BY thay vì
    /// nạp toàn bộ OrderDetails. Chỉ là dữ liệu hiển thị; quyền sở hữu đã được service lọc
    /// từ trước nên danh sách id ở đây vốn đã chỉ thuộc về khách đang đăng nhập.
    /// </summary>
    private async Task<Dictionary<int, int>> CountLinesAsync(
        IReadOnlyList<Order> orders, CancellationToken cancellationToken)
    {
        if (orders.Count == 0)
        {
            return [];
        }

        var orderIds = orders.Select(order => order.Id).ToList();

        return await _db.OrderDetails
            .AsNoTracking()
            .Where(detail => orderIds.Contains(detail.OrderId))
            .GroupBy(detail => detail.OrderId)
            .Select(group => new { OrderId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.OrderId, row => row.Count, cancellationToken);
    }
}
