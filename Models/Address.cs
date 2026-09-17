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

    /// <summary>
    /// Ward name as it was chosen, e.g. "Phường Ba Đình".
    /// </summary>
    /// <remarks>
    /// The name is stored next to <see cref="WardCode"/> on purpose (ADDR-02): a later
    /// dataset update may rename or merge a unit, and an address the customer already saved
    /// must keep reading the way they entered it.
    /// </remarks>
    public string Ward { get; set; } = string.Empty;

    /// <summary>
    /// Five digit ward code from the national statistics office, e.g. "00004".
    /// Empty for rows created before ADDR-02 introduced the selectors.
    /// </summary>
    public string WardCode { get; set; } = string.Empty;

    /// <summary>
    /// District name. Null since the two tier reform of 01/07/2025 removed this level —
    /// kept nullable for rows written before then and for shipping APIs that still ask
    /// for a district.
    /// </summary>
    public string? District { get; set; }

    /// <summary>Province or city name as chosen, e.g. "Thành phố Hà Nội".</summary>
    public string Province { get; set; } = string.Empty;

    /// <summary>
    /// Two digit province code from the national statistics office, e.g. "01".
    /// Empty for rows created before ADDR-02 introduced the selectors.
    /// </summary>
    public string ProvinceCode { get; set; } = string.Empty;

    /// <summary>Pre-selected at checkout. At most one per user.</summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// The address parts joined into one line. Not a column — checkout copies this into
    /// <see cref="Order.ShippingAddress"/> so the order keeps its own snapshot.
    /// District is skipped when empty, which is the normal case since the 2025 reform.
    /// </summary>
    public string FullAddress => string.Join(", ", new[] { AddressLine, Ward, District, Province }
        .Where(part => !string.IsNullOrWhiteSpace(part)));

    public ApplicationUser User { get; set; } = null!;
}
