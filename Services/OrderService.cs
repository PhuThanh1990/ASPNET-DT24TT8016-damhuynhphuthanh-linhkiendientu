using System.Security.Cryptography;
using ElectronicStore.Data;
using ElectronicStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ElectronicStore.Services;

/// <inheritdoc cref="IOrderService"/>
public sealed class OrderService : IOrderService
{
    /// <summary>Flat delivery fee, waived from <see cref="FreeShippingThreshold"/> upwards.</summary>
    /// <remarks>
    /// Placeholder pricing so the total is always computed on the server. When the shop wants
    /// real rates, this is the only place to change.
    /// </remarks>
    private const decimal FlatShippingFee = 30_000m;

    private const decimal FreeShippingThreshold = 500_000m;

    /// <summary>Sanity caps on input; the real limit is always the stock on hand.</summary>
    private const int MaxDistinctItems = 50;

    private const int MaxQuantityPerItem = 999;

    // Column lengths from OrderConfiguration. Checked here so an over-long value comes back
    // as a readable message instead of a truncation error from SQL Server.
    private const int ShippingFullNameMaxLength = 100;
    private const int ShippingPhoneMaxLength = 20;
    private const int ShippingAddressMaxLength = 500;
    private const int NoteMaxLength = 500;

    /// <summary>32 unambiguous characters, so a random byte maps to one of them without bias.</summary>
    private const string CodeAlphabet = "0123456789ABCDEFGHJKLMNPQRSTUVWX";

    private readonly ApplicationDbContext _db;
    private readonly ILogger<OrderService> _logger;

    public OrderService(ApplicationDbContext db, ILogger<OrderService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ---------------------------------------------------------------- place an order

    public async Task<OrderResult<Order>> PlaceOrderAsync(
        PlaceOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return OrderResult<Order>.Fail(OrderErrorCode.Forbidden, "Bạn cần đăng nhập để đặt hàng.");
        }

        var buyerExists = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.UserId && u.IsActive, cancellationToken);

        if (!buyerExists)
        {
            return OrderResult<Order>.Fail(OrderErrorCode.Forbidden,
                "Tài khoản không tồn tại hoặc đã bị khóa.");
        }

        var itemsResult = NormalizeItems(request.Items);
        if (!itemsResult.Succeeded)
        {
            return OrderResult<Order>.FailLike(itemsResult);
        }

        var items = itemsResult.Value!;

        var shippingResult = await ResolveShippingAsync(request, cancellationToken);
        if (!shippingResult.Succeeded)
        {
            return OrderResult<Order>.FailLike(shippingResult);
        }

        var shipping = shippingResult.Value!;

        var note = request.Note?.Trim();
        if (note is { Length: > NoteMaxLength })
        {
            return OrderResult<Order>.Fail(OrderErrorCode.ValidationFailed,
                $"Ghi chú tối đa {NoteMaxLength} ký tự.");
        }

        // Everything below is one unit of work: either the order, its lines and the stock
        // decrements all land, or none of them do. Disposing the transaction without a
        // commit rolls it back, which is what every early return below relies on.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var productIds = items.Keys.ToList();

            // Prices and names come from the database, never from the caller.
            var products = await _db.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new ProductSnapshot(p.Id, p.Name, p.Price, p.StockQuantity, p.IsActive))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            var problems = ValidateAvailability(items, products);
            if (problems.Count > 0)
            {
                return OrderResult<Order>.Fail(OrderErrorCode.Conflict, problems);
            }

            var now = DateTime.UtcNow;
            var order = new Order
            {
                OrderCode = GenerateOrderCode(now),
                UserId = request.UserId,
                AddressId = shipping.AddressId,
                ShippingFullName = shipping.FullName,
                ShippingPhone = shipping.Phone,
                ShippingAddress = shipping.Address,
                Note = string.IsNullOrWhiteSpace(note) ? null : note,
                Status = OrderStatus.Pending,
                CreatedAt = now
            };

            decimal subTotal = 0m;

            foreach (var (productId, quantity) in items)
            {
                var product = products[productId];
                var lineTotal = product.Price * quantity;
                subTotal += lineTotal;

                order.OrderDetails.Add(new OrderDetail
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Quantity = quantity,
                    LineTotal = lineTotal
                });
            }

            order.SubTotal = subTotal;
            order.ShippingFee = CalculateShippingFee(subTotal);
            order.TotalAmount = order.SubTotal + order.ShippingFee;

            // The check above only proves the stock was sufficient a moment ago. This is the
            // statement that actually reserves it: a single conditional UPDATE per product,
            // so SQL Server serialises concurrent buyers on the row lock and stock can never
            // go negative. Zero rows affected means somebody else took the last units.
            // Products are touched in id order so two concurrent orders sharing products
            // always lock them in the same sequence and cannot deadlock each other.
            foreach (var (productId, quantity) in items.OrderBy(pair => pair.Key))
            {
                var reserved = await _db.Products
                    .Where(p => p.Id == productId && p.IsActive && p.StockQuantity >= quantity)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity),
                        cancellationToken);

                if (reserved == 0)
                {
                    var name = products[productId].Name;
                    return OrderResult<Order>.Fail(OrderErrorCode.Conflict,
                        $"\"{name}\" vừa được khách khác mua hết. Vui lòng cập nhật lại giỏ hàng.");
                }
            }

            _db.Orders.Add(order);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Order {OrderCode} placed by {UserId} with {LineCount} lines, total {Total}.",
                order.OrderCode, order.UserId, order.OrderDetails.Count, order.TotalAmount);

            return OrderResult<Order>.Ok(order);
        }
        catch (DbUpdateException ex)
        {
            // Rollback happens when the transaction is disposed. The database detail stays in
            // the log; the customer gets a message they can act on.
            _logger.LogError(ex, "Placing an order for {UserId} failed while writing to the database.",
                request.UserId);

            return OrderResult<Order>.Fail(OrderErrorCode.Unexpected,
                "Không thể tạo đơn hàng lúc này. Vui lòng thử lại.");
        }
    }

    // ---------------------------------------------------------------- reads

    public async Task<OrderResult<Order>> GetForCustomerAsync(
        int orderId, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return OrderResult<Order>.Fail(OrderErrorCode.Forbidden, "Bạn cần đăng nhập.");
        }

        var order = await OrdersWithDetails()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            return OrderResult<Order>.Fail(OrderErrorCode.NotFound, "Không tìm thấy đơn hàng.");
        }

        // Same message as "not found" on purpose: probing ids must not reveal that an order
        // exists under another account.
        if (!string.Equals(order.UserId, userId, StringComparison.Ordinal))
        {
            return OrderResult<Order>.Fail(OrderErrorCode.NotFound, "Không tìm thấy đơn hàng.");
        }

        return OrderResult<Order>.Ok(order);
    }

    public async Task<OrderResult<Order>> GetForAdminAsync(
        int orderId, CancellationToken cancellationToken = default)
    {
        var order = await OrdersWithDetails()
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        return order is null
            ? OrderResult<Order>.Fail(OrderErrorCode.NotFound, "Không tìm thấy đơn hàng.")
            : OrderResult<Order>.Ok(order);
    }

    public Task<PagedResult<Order>> GetCustomerOrdersAsync(
        string userId, OrderQuery query, CancellationToken cancellationToken = default)
    {
        // Keyword search is an admin feature; a customer list is always scoped to one account.
        var source = _db.Orders.AsNoTracking().Where(o => o.UserId == userId);

        if (query.Status is { } status)
        {
            source = source.Where(o => o.Status == status);
        }

        return ToPagedResultAsync(source, query, cancellationToken);
    }

    public Task<PagedResult<Order>> GetAdminOrdersAsync(
        OrderQuery query, CancellationToken cancellationToken = default)
    {
        var source = _db.Orders.AsNoTracking().Include(o => o.User).AsQueryable();

        if (query.Status is { } status)
        {
            source = source.Where(o => o.Status == status);
        }

        var keyword = query.Keyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            source = source.Where(o =>
                o.OrderCode.Contains(keyword) ||
                o.ShippingFullName.Contains(keyword) ||
                o.ShippingPhone.Contains(keyword));
        }

        return ToPagedResultAsync(source, query, cancellationToken);
    }

    /// <inheritdoc />
    public decimal QuoteShippingFee(decimal subTotal) => CalculateShippingFee(subTotal);

    // ---------------------------------------------------------------- lifecycle

    public async Task<OrderResult<Order>> UpdateStatusAsync(
        int orderId, OrderStatus newStatus, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(newStatus))
        {
            return OrderResult<Order>.Fail(OrderErrorCode.ValidationFailed, "Trạng thái không hợp lệ.");
        }

        if (newStatus == OrderStatus.Cancelled)
        {
            // Cancelling has to give the stock back, which this method does not do.
            return OrderResult<Order>.Fail(OrderErrorCode.ValidationFailed,
                "Hủy đơn phải dùng chức năng hủy riêng để hoàn lại tồn kho.");
        }

        var current = await _db.Orders
            .AsNoTracking()
            .Select(o => new { o.Id, o.Status, o.OrderCode })
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (current is null)
        {
            return OrderResult<Order>.Fail(OrderErrorCode.NotFound, "Không tìm thấy đơn hàng.");
        }

        if (!current.Status.CanTransitionTo(newStatus))
        {
            return OrderResult<Order>.Fail(OrderErrorCode.Conflict,
                $"Không thể chuyển đơn {current.OrderCode} từ {current.Status} sang {newStatus}.");
        }

        // Compare-and-swap on the status column: the update only applies if the order is
        // still in the state that was just validated, so two admins clicking at the same
        // time cannot both advance it.
        var updated = await _db.Orders
            .Where(o => o.Id == orderId && o.Status == current.Status)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(o => o.Status, newStatus)
                    .SetProperty(o => o.UpdatedAt, DateTime.UtcNow),
                cancellationToken);

        if (updated == 0)
        {
            return OrderResult<Order>.Fail(OrderErrorCode.Conflict,
                "Đơn hàng vừa được cập nhật bởi thao tác khác. Vui lòng tải lại trang.");
        }

        _logger.LogInformation("Order {OrderCode} moved from {From} to {To}.",
            current.OrderCode, current.Status, newStatus);

        return await GetForAdminAsync(orderId, cancellationToken);
    }

    public Task<OrderResult<Order>> CancelByCustomerAsync(
        int orderId, string userId, CancellationToken cancellationToken = default) =>
        CancelAsync(orderId, userId, cancellationToken);

    public Task<OrderResult<Order>> CancelByAdminAsync(
        int orderId, CancellationToken cancellationToken = default) =>
        CancelAsync(orderId, requestingUserId: null, cancellationToken);

    /// <summary>
    /// Cancels an order and returns its stock exactly once.
    /// </summary>
    /// <param name="requestingUserId">
    /// The customer who must own the order, or null for an admin who may cancel any order.
    /// </param>
    private async Task<OrderResult<Order>> CancelAsync(
        int orderId, string? requestingUserId, CancellationToken cancellationToken)
    {
        var isAdmin = requestingUserId is null;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var order = await _db.Orders
                .AsNoTracking()
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order is null)
            {
                return OrderResult<Order>.Fail(OrderErrorCode.NotFound, "Không tìm thấy đơn hàng.");
            }

            if (!isAdmin && !string.Equals(order.UserId, requestingUserId, StringComparison.Ordinal))
            {
                return OrderResult<Order>.Fail(OrderErrorCode.NotFound, "Không tìm thấy đơn hàng.");
            }

            var mayCancel = isAdmin
                ? order.Status.CanBeCancelledByAdmin()
                : order.Status.CanBeCancelledByCustomer();

            if (!mayCancel)
            {
                return OrderResult<Order>.Fail(OrderErrorCode.Conflict, DescribeCancelRefusal(order.Status, isAdmin));
            }

            // The whole idempotency guarantee lives in this one statement: the status column
            // is both the state and the lock. Only the caller that moves the order out of its
            // current status gets a row back, and only that caller restores stock — so a
            // double-submitted cancel can never add the quantities twice.
            var cancelled = await _db.Orders
                .Where(o => o.Id == orderId && o.Status == order.Status)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(o => o.Status, OrderStatus.Cancelled)
                        .SetProperty(o => o.UpdatedAt, DateTime.UtcNow),
                    cancellationToken);

            if (cancelled == 0)
            {
                return OrderResult<Order>.Fail(OrderErrorCode.Conflict,
                    "Đơn hàng vừa được cập nhật bởi thao tác khác. Vui lòng tải lại trang.");
            }

            foreach (var detail in order.OrderDetails.OrderBy(d => d.ProductId))
            {
                var quantity = detail.Quantity;
                var productId = detail.ProductId;

                await _db.Products
                    .Where(p => p.Id == productId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantity),
                        cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Order {OrderCode} cancelled by {Actor}; stock restored for {LineCount} lines.",
                order.OrderCode, isAdmin ? "admin" : order.UserId, order.OrderDetails.Count);

            return await GetForAdminAsync(orderId, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Cancelling order {OrderId} failed while writing to the database.", orderId);

            return OrderResult<Order>.Fail(OrderErrorCode.Unexpected,
                "Không thể hủy đơn hàng lúc này. Vui lòng thử lại.");
        }
    }

    // ---------------------------------------------------------------- helpers

    private IQueryable<Order> OrdersWithDetails() =>
        _db.Orders.AsNoTracking().Include(o => o.OrderDetails);

    private static async Task<PagedResult<Order>> ToPagedResultAsync(
        IQueryable<Order> source, OrderQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, OrderQuery.MaxPageSize);

        var totalCount = await source.CountAsync(cancellationToken);

        var items = await source
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Order>(items, page, pageSize, totalCount);
    }

    /// <summary>
    /// Turns the raw lines into one entry per product. A cart that lists the same product
    /// twice is a normal thing to receive, so the quantities are merged rather than rejected —
    /// validating them separately would let 2 + 3 pass against a stock of 4.
    /// </summary>
    private static OrderResult<Dictionary<int, int>> NormalizeItems(IReadOnlyList<OrderItemRequest> rawItems)
    {
        if (rawItems is null || rawItems.Count == 0)
        {
            return OrderResult<Dictionary<int, int>>.Fail(OrderErrorCode.ValidationFailed,
                "Giỏ hàng đang trống.");
        }

        var errors = new List<string>();
        var merged = new Dictionary<int, int>();

        foreach (var item in rawItems)
        {
            if (item.ProductId <= 0)
            {
                errors.Add("Giỏ hàng chứa sản phẩm không hợp lệ.");
                continue;
            }

            if (item.Quantity <= 0)
            {
                errors.Add($"Số lượng của sản phẩm #{item.ProductId} phải lớn hơn 0.");
                continue;
            }

            merged.TryGetValue(item.ProductId, out var current);
            merged[item.ProductId] = current + item.Quantity;
        }

        foreach (var (productId, quantity) in merged)
        {
            if (quantity > MaxQuantityPerItem)
            {
                errors.Add($"Số lượng của sản phẩm #{productId} tối đa {MaxQuantityPerItem}.");
            }
        }

        if (merged.Count > MaxDistinctItems)
        {
            errors.Add($"Một đơn hàng tối đa {MaxDistinctItems} sản phẩm khác nhau.");
        }

        if (errors.Count > 0)
        {
            return OrderResult<Dictionary<int, int>>.Fail(OrderErrorCode.ValidationFailed, errors);
        }

        return OrderResult<Dictionary<int, int>>.Ok(merged);
    }

    /// <summary>Collects every availability problem so the customer sees them all at once.</summary>
    private static List<string> ValidateAvailability(
        Dictionary<int, int> items, IReadOnlyDictionary<int, ProductSnapshot> products)
    {
        var problems = new List<string>();

        foreach (var (productId, quantity) in items)
        {
            if (!products.TryGetValue(productId, out var product))
            {
                problems.Add($"Sản phẩm #{productId} không còn tồn tại.");
                continue;
            }

            if (!product.IsActive)
            {
                problems.Add($"\"{product.Name}\" đã ngừng kinh doanh.");
                continue;
            }

            if (product.StockQuantity <= 0)
            {
                problems.Add($"\"{product.Name}\" đã hết hàng.");
                continue;
            }

            if (quantity > product.StockQuantity)
            {
                problems.Add($"\"{product.Name}\" chỉ còn {product.StockQuantity} sản phẩm, bạn đang đặt {quantity}.");
            }
        }

        return problems;
    }

    /// <summary>
    /// Builds the shipping snapshot. Using an address book entry is preferred because the
    /// values then come from the database after an ownership check, instead of from the form.
    /// </summary>
    private async Task<OrderResult<ShippingSnapshot>> ResolveShippingAsync(
        PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.AddressId is { } addressId)
        {
            var address = await _db.ShippingAddresses
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == addressId, cancellationToken);

            if (address is null)
            {
                return OrderResult<ShippingSnapshot>.Fail(OrderErrorCode.NotFound,
                    "Địa chỉ giao hàng không tồn tại.");
            }

            if (!string.Equals(address.UserId, request.UserId, StringComparison.Ordinal))
            {
                return OrderResult<ShippingSnapshot>.Fail(OrderErrorCode.Forbidden,
                    "Địa chỉ giao hàng không thuộc về tài khoản này.");
            }

            var line = address.FullAddress;
            if (line.Length > ShippingAddressMaxLength)
            {
                return OrderResult<ShippingSnapshot>.Fail(OrderErrorCode.ValidationFailed,
                    $"Địa chỉ giao hàng quá dài (tối đa {ShippingAddressMaxLength} ký tự).");
            }

            return OrderResult<ShippingSnapshot>.Ok(
                new ShippingSnapshot(address.Id, address.ReceiverName, address.PhoneNumber, line));
        }

        var fullName = request.ShippingFullName?.Trim();
        var phone = request.ShippingPhone?.Trim();
        var addressLine = request.ShippingAddress?.Trim();

        var errors = new List<string>();

        if (string.IsNullOrEmpty(fullName))
        {
            errors.Add("Vui lòng nhập tên người nhận.");
        }
        else if (fullName.Length > ShippingFullNameMaxLength)
        {
            errors.Add($"Tên người nhận tối đa {ShippingFullNameMaxLength} ký tự.");
        }

        if (string.IsNullOrEmpty(phone))
        {
            errors.Add("Vui lòng nhập số điện thoại người nhận.");
        }
        else if (phone.Length > ShippingPhoneMaxLength)
        {
            errors.Add($"Số điện thoại tối đa {ShippingPhoneMaxLength} ký tự.");
        }

        if (string.IsNullOrEmpty(addressLine))
        {
            errors.Add("Vui lòng nhập địa chỉ giao hàng.");
        }
        else if (addressLine.Length > ShippingAddressMaxLength)
        {
            errors.Add($"Địa chỉ giao hàng tối đa {ShippingAddressMaxLength} ký tự.");
        }

        return errors.Count > 0
            ? OrderResult<ShippingSnapshot>.Fail(OrderErrorCode.ValidationFailed, errors)
            : OrderResult<ShippingSnapshot>.Ok(new ShippingSnapshot(null, fullName!, phone!, addressLine!));
    }

    private static decimal CalculateShippingFee(decimal subTotal) =>
        subTotal >= FreeShippingThreshold ? 0m : FlatShippingFee;

    /// <summary>
    /// Builds a code like <c>DH20260911-7K2QF4XB</c>: 19 characters, inside the 20 character
    /// column. The random tail avoids a per-day counter, which two simultaneous checkouts
    /// would race on, and it keeps codes unguessable so one customer cannot walk another
    /// customer's order codes. The unique index on OrderCode remains the final guarantee.
    /// </summary>
    private static string GenerateOrderCode(DateTime nowUtc)
    {
        Span<byte> randomBytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(randomBytes);

        Span<char> suffix = stackalloc char[8];
        for (var i = 0; i < suffix.Length; i++)
        {
            suffix[i] = CodeAlphabet[randomBytes[i] % CodeAlphabet.Length];
        }

        return $"DH{nowUtc:yyyyMMdd}-{new string(suffix)}";
    }

    private static string DescribeCancelRefusal(OrderStatus status, bool isAdmin) => status switch
    {
        OrderStatus.Cancelled => "Đơn hàng đã được hủy trước đó.",
        OrderStatus.Completed => "Đơn hàng đã giao thành công nên không thể hủy.",
        OrderStatus.Shipping => "Đơn hàng đang được giao nên không thể hủy.",
        _ when !isAdmin => "Đơn hàng đã được xác nhận. Vui lòng liên hệ shop để hủy.",
        _ => $"Không thể hủy đơn hàng ở trạng thái {status}."
    };

    private sealed record ProductSnapshot(int Id, string Name, decimal Price, int StockQuantity, bool IsActive);

    private sealed record ShippingSnapshot(int? AddressId, string FullName, string Phone, string Address);
}
