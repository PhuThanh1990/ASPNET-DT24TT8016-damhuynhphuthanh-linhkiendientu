using ElectronicStore.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicStore.ViewComponents;

/// <summary>
/// Badge số lượng giỏ hàng trên navbar. Chỉ đọc Session, không chạm database, nên không
/// làm chậm mọi trang.
/// </summary>
public class CartSummaryViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        return View(CartSession.GetTotalQuantity(HttpContext.Session));
    }
}
