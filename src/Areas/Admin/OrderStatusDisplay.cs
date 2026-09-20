using ElectronicStore.Models;

namespace ElectronicStore.Areas.Admin;

/// <summary>
/// How an <see cref="OrderStatus"/> is rendered in the Admin screens: Vietnamese label,
/// Bootstrap badge class and button caption.
/// </summary>
/// <remarks>
/// Presentation only — deliberately contains no workflow knowledge. Which statuses an order
/// may move to, and whether it may still be cancelled, are business rules and live in
/// <see cref="OrderStatusRules"/> (Core). A view asks Core what is allowed and asks this
/// helper how to draw it, so the two can never drift apart.
/// </remarks>
public static class OrderStatusDisplay
{
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

    /// <summary>
    /// Caption of the button that moves an order <em>into</em> <paramref name="target"/>,
    /// e.g. "Xác nhận đơn" for <see cref="OrderStatus.Confirmed"/>.
    /// </summary>
    /// <remarks>
    /// Keyed on the destination rather than on the current status: the caller already gets
    /// the destination from <see cref="OrderStatusRules.AllowedNextStatuses"/>, so this
    /// helper never has to know which step follows which.
    /// </remarks>
    public static string ActionLabel(OrderStatus target) => target switch
    {
        OrderStatus.Confirmed => "Xác nhận đơn",
        OrderStatus.Preparing => "Bắt đầu chuẩn bị",
        OrderStatus.Shipping => "Bàn giao vận chuyển",
        OrderStatus.Completed => "Hoàn tất giao hàng",
        OrderStatus.Cancelled => "Hủy đơn",
        _ => DisplayName(target)
    };

    /// <summary>Every status in lifecycle order — used to build the filter bar.</summary>
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
