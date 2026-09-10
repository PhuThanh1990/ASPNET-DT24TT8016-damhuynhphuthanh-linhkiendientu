using ElectronicStore.Models.ViewModels;

namespace ElectronicStore.Helpers;

/// <summary>
/// CUS-12 — nơi duy nhất đọc/ghi giỏ hàng trong Session, để controller và view component
/// dùng chung đúng một khóa và đúng một định dạng.
/// </summary>
public static class CartSession
{
    private const string CartKey = "cart";

    /// <summary>Giỏ hàng hiện tại; chưa có gì thì trả về danh sách rỗng, không null.</summary>
    public static List<CartItemViewModel> GetItems(ISession session) =>
        session.GetObject<List<CartItemViewModel>>(CartKey) ?? [];

    public static void SaveItems(ISession session, List<CartItemViewModel> items)
    {
        if (items.Count == 0)
        {
            session.Remove(CartKey);
            return;
        }

        session.SetObject(CartKey, items);
    }

    /// <summary>Tổng số lượng cho badge trên navbar; không chạm tới database.</summary>
    public static int GetTotalQuantity(ISession session) =>
        GetItems(session).Sum(item => item.Quantity);
}
