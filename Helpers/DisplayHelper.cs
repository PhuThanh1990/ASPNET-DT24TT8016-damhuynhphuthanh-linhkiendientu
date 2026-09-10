using System.Globalization;

namespace ElectronicStore.Helpers;

/// <summary>
/// Small formatting helpers shared by the customer views. Deliberately static and
/// dependency-free — this is presentation formatting, not business logic.
/// </summary>
public static class DisplayHelper
{
    /// <summary>Placeholder drawn when a product has no image row at all.</summary>
    public const string NoImageUrl = "/images/no-image.svg";

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Formats a price the Vietnamese way, e.g. 1250000 -> "1.250.000 ₫".</summary>
    public static string ToVnd(this decimal value) =>
        value.ToString("#,##0", Vietnamese) + " ₫";

    /// <summary>Formats an optional price; returns an empty string when there is none.</summary>
    public static string ToVnd(this decimal? value) =>
        value.HasValue ? value.Value.ToVnd() : string.Empty;

    /// <summary>Returns the image path, falling back to the placeholder when missing.</summary>
    public static string ImageOrPlaceholder(string? imageUrl) =>
        string.IsNullOrWhiteSpace(imageUrl) ? NoImageUrl : imageUrl;
}
