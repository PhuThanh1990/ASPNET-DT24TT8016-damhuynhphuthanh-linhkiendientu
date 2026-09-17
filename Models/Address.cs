namespace ElectronicStore.Models;

/// <summary>
/// An entry in a customer's shipping address book.
/// Mapping lives in <c>Data/Configurations/AddressConfiguration.cs</c>.
/// </summary>
public class Address
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>Receiver name, which may differ from the account owner.</summary>
    public string FullName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>House number and street.</summary>
    public string AddressLine { get; set; } = string.Empty;

    public string Ward { get; set; } = string.Empty;

    public string District { get; set; } = string.Empty;

    public string Province { get; set; } = string.Empty;

    /// <summary>Pre-selected at checkout. At most one per user.</summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// The four address parts joined into one line. Not a column — checkout copies this
    /// into <see cref="Order.ShippingAddress"/> so the order keeps its own snapshot.
    /// </summary>
    public string FullAddress => $"{AddressLine}, {Ward}, {District}, {Province}";

    public ApplicationUser User { get; set; } = null!;
}
