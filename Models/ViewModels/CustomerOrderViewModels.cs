namespace ElectronicStore.Models.ViewModels;

/// <summary>CUS-19 — một trang lịch sử đơn hàng của khách đang đăng nhập.</summary>
public class CustomerOrderListViewModel
{
    public IReadOnlyList<CustomerOrderSummaryViewModel> Orders { get; set; } = [];

    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }

    public bool HasPreviousPage => CurrentPage > 1;

    public bool HasNextPage => CurrentPage < TotalPages;

    public bool IsEmpty => Orders.Count == 0;
}

/// <summary>Một dòng trong danh sách "Đơn hàng của tôi".</summary>
public class CustomerOrderSummaryViewModel
{
    public int Id { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public decimal TotalAmount { get; set; }

    public OrderStatus Status { get; set; }

    public int ItemCount { get; set; }

    public string StatusText => OrderStatusPresentation.Text(Status);

    public string StatusBadgeClass => OrderStatusPresentation.BadgeClass(Status);
}

/// <summary>
/// CUS-18/CUS-19 — chi tiết một đơn hàng, dùng chung cho trang "Đặt hàng thành công" và
/// trang chi tiết đơn.
/// </summary>
/// <remarks>
/// Mọi số liệu đều là bản chụp đã lưu trong Order/OrderDetail lúc đặt hàng. View không tra
/// lại bảng Product, nên sau này shop đổi tên hay đổi giá sản phẩm thì đơn cũ vẫn hiển thị
/// đúng những gì khách đã mua.
/// </remarks>
public class CustomerOrderDetailViewModel
{
    public int Id { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public OrderStatus Status { get; set; }

    public string ShippingFullName { get; set; } = string.Empty;

    public string ShippingPhone { get; set; } = string.Empty;

    public string ShippingAddress { get; set; } = string.Empty;

    public string? Note { get; set; }

    public decimal SubTotal { get; set; }

    public decimal ShippingFee { get; set; }

    public decimal TotalAmount { get; set; }

    public IReadOnlyList<CustomerOrderLineViewModel> Lines { get; set; } = [];

    public string StatusText => OrderStatusPresentation.Text(Status);

    public string StatusBadgeClass => OrderStatusPresentation.BadgeClass(Status);

    public int TotalQuantity => Lines.Sum(line => line.Quantity);

    /// <summary>
    /// Nút "Hủy đơn" chỉ hiện khi Core cho phép. Luật nằm ở <see cref="OrderStatusRules"/>
    /// và được service kiểm tra lại khi bấm, view chỉ hỏi lại đúng luật đó.
    /// </summary>
    public bool CanCancel => Status.CanBeCancelledByCustomer();

    /// <summary>Chuyển entity Order (do IOrderService trả về) thành dữ liệu cho view.</summary>
    public static CustomerOrderDetailViewModel FromOrder(Order order) => new()
    {
        Id = order.Id,
        OrderCode = order.OrderCode,
        CreatedAt = order.CreatedAt,
        Status = order.Status,
        ShippingFullName = order.ShippingFullName,
        ShippingPhone = order.ShippingPhone,
        ShippingAddress = order.ShippingAddress,
        Note = order.Note,
        SubTotal = order.SubTotal,
        ShippingFee = order.ShippingFee,
        TotalAmount = order.TotalAmount,
        Lines = order.OrderDetails
            .OrderBy(detail => detail.Id)
            .Select(detail => new CustomerOrderLineViewModel
            {
                ProductId = detail.ProductId,
                ProductName = detail.ProductName,
                UnitPrice = detail.UnitPrice,
                Quantity = detail.Quantity,
                LineTotal = detail.LineTotal,
            })
            .ToList(),
    };
}

/// <summary>Một dòng hàng đã chốt trong đơn — tên và giá là bản chụp lúc đặt.</summary>
public class CustomerOrderLineViewModel
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal { get; set; }
}

/// <summary>
/// Nhãn tiếng Việt và class badge cho trạng thái đơn ở phía khách hàng.
/// </summary>
/// <remarks>
/// Thuần trình bày, không chứa luật nghiệp vụ: chuyển trạng thái và quyền hủy đơn hỏi
/// <see cref="OrderStatusRules"/> (Core). Khu vực Admin có bảng nhãn riêng ở
/// <c>Areas/Admin/OrderStatusDisplay.cs</c>; hai bảng nên được gộp lại khi có ai đó dọn
/// sang một helper dùng chung — ở đây không tham chiếu sang namespace của Admin để phía
/// khách hàng không phụ thuộc vào area của người khác.
/// </remarks>
internal static class OrderStatusPresentation
{
    public static string Text(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Chờ xác nhận",
        OrderStatus.Confirmed => "Đã xác nhận",
        OrderStatus.Preparing => "Đang chuẩn bị",
        OrderStatus.Shipping => "Đang giao",
        OrderStatus.Completed => "Hoàn tất",
        OrderStatus.Cancelled => "Đã hủy",
        _ => status.ToString(),
    };

    public static string BadgeClass(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "text-bg-warning",
        OrderStatus.Confirmed => "text-bg-info",
        OrderStatus.Preparing => "text-bg-primary",
        OrderStatus.Shipping => "text-bg-secondary",
        OrderStatus.Completed => "text-bg-success",
        OrderStatus.Cancelled => "text-bg-danger",
        _ => "text-bg-light",
    };
}
