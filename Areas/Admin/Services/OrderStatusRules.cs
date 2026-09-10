using ElectronicStore.Models;

namespace ElectronicStore.Areas.Admin.Services;

/// <summary>
/// The order lifecycle exactly as documented in docs/database-schema.md § 9.2:
/// <code>
/// Pending → Confirmed → Preparing → Shipping → Completed
///    │          │           │
///    └──────────┴───────────┴──────────→ Cancelled
/// </code>
/// <c>Completed</c> and <c>Cancelled</c> are terminal.
/// </summary>
/// <remarks>
/// This table drives the Admin UI so the buttons can never offer an illegal jump. It is the
/// shape of the workflow, not its side effects — the moment CORE-16/18 lands, the
/// authoritative copy belongs to the Core OrderService and this file should defer to it.
/// </remarks>
public static class OrderStatusRules
{
    /// <summary>The single status an order may move forward to, or null when it is terminal.</summary>
    public static OrderStatus? NextStatus(OrderStatus current) => current switch
    {
        OrderStatus.Pending => OrderStatus.Confirmed,
        OrderStatus.Confirmed => OrderStatus.Preparing,
        OrderStatus.Preparing => OrderStatus.Shipping,
        OrderStatus.Shipping => OrderStatus.Completed,
        _ => null
    };

    /// <summary>An order may still be cancelled until it has been handed to the carrier.</summary>
    public static bool CanCancel(OrderStatus current) =>
        current is OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.Preparing;

    /// <summary>True when <paramref name="target"/> is a legal move from <paramref name="current"/>.</summary>
    public static bool CanMoveTo(OrderStatus current, OrderStatus target) =>
        target == OrderStatus.Cancelled ? CanCancel(current) : NextStatus(current) == target;

    /// <summary>Label of the button that performs the forward move, e.g. "Xác nhận đơn".</summary>
    public static string? ForwardActionLabel(OrderStatus current) => current switch
    {
        OrderStatus.Pending => "Xác nhận đơn",
        OrderStatus.Confirmed => "Bắt đầu chuẩn bị",
        OrderStatus.Preparing => "Bàn giao vận chuyển",
        OrderStatus.Shipping => "Hoàn tất giao hàng",
        _ => null
    };

    public static string DisplayName(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Chờ xác nhận",
        OrderStatus.Confirmed => "Đã xác nhận",
        OrderStatus.Preparing => "Đang chuẩn bị",
        OrderStatus.Shipping => "Đang giao",
        OrderStatus.Completed => "Hoàn tất",
        OrderStatus.Cancelled => "Đã hủy",
        _ => status.ToString()
    };

    /// <summary>Bootstrap badge class used by the list and detail screens.</summary>
    public static string BadgeClass(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "text-bg-warning",
        OrderStatus.Confirmed => "text-bg-info",
        OrderStatus.Preparing => "text-bg-primary",
        OrderStatus.Shipping => "text-bg-secondary",
        OrderStatus.Completed => "text-bg-success",
        OrderStatus.Cancelled => "text-bg-danger",
        _ => "text-bg-light"
    };

    /// <summary>Every status, in lifecycle order — used to build the filter dropdown.</summary>
    public static IReadOnlyList<OrderStatus> All { get; } =
    [
        OrderStatus.Pending,
        OrderStatus.Confirmed,
        OrderStatus.Preparing,
        OrderStatus.Shipping,
        OrderStatus.Completed,
        OrderStatus.Cancelled
    ];
}
