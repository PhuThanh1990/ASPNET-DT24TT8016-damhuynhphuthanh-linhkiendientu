namespace ElectronicStore.Models;

/// <summary>
/// One line of an order. Written once at checkout and never updated: the product name and
/// unit price are copies, so renaming or repricing a product cannot change past invoices.
/// Mapping lives in <c>Data/Configurations/OrderDetailConfiguration.cs</c>.
/// </summary>
public class OrderDetail
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    /// <summary>Kept so the customer can open the product page again; never used for display.</summary>
    public int ProductId { get; set; }

    /// <summary>Product name as it was when the order was placed.</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Selling price as it was when the order was placed.</summary>
    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    /// <summary><see cref="UnitPrice"/> * <see cref="Quantity"/>, stored so the total is frozen too.</summary>
    public decimal LineTotal { get; set; }

    public Order Order { get; set; } = null!;

    public Product Product { get; set; } = null!;
}
