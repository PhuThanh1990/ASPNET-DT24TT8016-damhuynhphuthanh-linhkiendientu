using ElectronicStore.Models;

namespace ElectronicStore.Services;

/// <summary>
/// Everything the service needs to place an order.
/// </summary>
/// <remarks>
/// There is deliberately no price, no line total and no order total on this type: money is
/// read from the database and computed by the service, so a tampered form cannot decide
/// what the customer pays. For the same reason <see cref="UserId"/> must be filled from the
/// signed-in principal by the controller, never from a form field.
/// </remarks>
public sealed class PlaceOrderRequest
{
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Entry from the customer's address book. When set, the shipping snapshot is taken
    /// from that row after checking it belongs to <see cref="UserId"/>, and the three
    /// Shipping* fields below are ignored.
    /// </summary>
    public int? AddressId { get; init; }

    /// <summary>Receiver name, used only when <see cref="AddressId"/> is null.</summary>
    public string? ShippingFullName { get; init; }

    /// <summary>Receiver phone, used only when <see cref="AddressId"/> is null.</summary>
    public string? ShippingPhone { get; init; }

    /// <summary>Full address on one line, used only when <see cref="AddressId"/> is null.</summary>
    public string? ShippingAddress { get; init; }

    public string? Note { get; init; }

    public IReadOnlyList<OrderItemRequest> Items { get; init; } = [];
}

/// <summary>One requested line. Quantity is a wish — stock decides the outcome.</summary>
public sealed record OrderItemRequest(int ProductId, int Quantity);

/// <summary>Filter and paging for the order lists.</summary>
public sealed class OrderQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public OrderStatus? Status { get; init; }

    /// <summary>Matches order code, receiver name or receiver phone. Admin list only.</summary>
    public string? Keyword { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;
}

/// <summary>One page of results plus what the caller needs to render a pager.</summary>
public sealed class PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    public IReadOnlyList<T> Items { get; }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}
