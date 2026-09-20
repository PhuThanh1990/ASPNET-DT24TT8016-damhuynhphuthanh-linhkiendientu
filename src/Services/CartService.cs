using System.Linq.Expressions;
using ElectronicStore.Data;
using ElectronicStore.Helpers;
using ElectronicStore.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Services;

/// <inheritdoc cref="ICartService"/>
public sealed class CartService : ICartService
{
    /// <summary>Trần số lượng cho mỗi dòng, để một sản phẩm không nuốt hết tồn kho.</summary>
    public const int MaxQuantityPerItem = 99;

    private readonly ApplicationDbContext _db;

    public CartService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CartViewModel> BuildAsync(ISession session, CancellationToken cancellationToken = default)
    {
        var items = CartSession.GetItems(session);
        var model = new CartViewModel();

        if (items.Count == 0)
        {
            return model;
        }

        var productIds = items.Select(item => item.ProductId).ToList();
        var products = await SellableProducts(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var changed = false;

        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || product.StockQuantity <= 0)
            {
                model.Notices.Add($"\"{item.Name}\" không còn bán nên đã được bỏ khỏi giỏ hàng.");
                changed = true;
                continue;
            }

            var limit = Math.Min(product.StockQuantity, MaxQuantityPerItem);
            var quantity = Math.Clamp(item.Quantity, 1, limit);

            if (quantity != item.Quantity)
            {
                model.Notices.Add($"\"{product.Name}\" chỉ còn {limit} sản phẩm, số lượng đã được điều chỉnh.");
                changed = true;
            }

            if (item.UnitPrice != product.Price)
            {
                model.Notices.Add($"Giá của \"{product.Name}\" đã thay đổi thành {product.Price.ToVnd()}.");
                changed = true;
            }

            Apply(item, product);
            item.Quantity = quantity;
            model.Items.Add(item);
        }

        if (changed)
        {
            CartSession.SaveItems(session, model.Items);
        }

        return model;
    }

    public Task<CartProduct?> FindSellableAsync(int productId, CancellationToken cancellationToken = default) =>
        SellableProducts(p => p.Id == productId).FirstOrDefaultAsync(cancellationToken);

    public void Clear(ISession session) => CartSession.SaveItems(session, []);

    /// <summary>Đồng bộ một dòng giỏ hàng theo dữ liệu database mới nhất (trừ số lượng).</summary>
    public static void Apply(CartItemViewModel item, CartProduct product)
    {
        item.Name = product.Name;
        item.Slug = product.Slug;
        item.ImageUrl = product.ImageUrl;
        item.UnitPrice = product.Price;
        item.StockQuantity = product.StockQuantity;
    }

    /// <summary>
    /// Chỉ lấy sản phẩm đang được bán, kèm ảnh đại diện bằng correlated subquery.
    /// </summary>
    /// <remarks>
    /// Điều kiện lọc phải nhận vào đây để áp lên <c>Product</c> TRƯỚC khi Select. EF Core
    /// không dịch được <c>.Select(p =&gt; new CartProduct(...)).Where(p =&gt; p.Id == x)</c>
    /// vì sau projection nó không còn ánh xạ ngược được về cột — với provider quan hệ thì
    /// câu đó ném InvalidOperationException ngay khi chạy.
    /// </remarks>
    private IQueryable<CartProduct> SellableProducts(Expression<Func<Models.Product, bool>> filter) =>
        _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Where(filter)
            .Select(p => new CartProduct(
                p.Id,
                p.Name,
                p.Slug,
                p.ProductImages
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.SortOrder)
                    .ThenBy(i => i.Id)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault(),
                p.Price,
                p.StockQuantity));
}
