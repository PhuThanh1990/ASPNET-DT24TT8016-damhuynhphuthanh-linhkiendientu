using System.Text.Json;
using ElectronicStore.Models.ViewModels;

namespace ElectronicStore.Helpers;

/// <summary>
/// CUS-07 — turns the <c>Product.Specifications</c> column into table rows.
///
/// The column holds a flat JSON object of string key/value pairs
/// (docs/database-schema.md § 6.1). Nothing in it is trusted: the value is admin-entered
/// free text, so it is returned as plain <see cref="string"/> and every view renders it
/// with Razor's default HTML encoding — never <c>Html.Raw</c>. Malformed JSON is not an
/// error for the customer, the page simply shows no specification table.
/// </summary>
public static class SpecificationParser
{
    /// <summary>Guard against a corrupted column producing an endless table.</summary>
    private const int MaxRows = 100;

    /// <summary>Guard against one value pushing the page over. Longer text is cut short.</summary>
    private const int MaxValueLength = 500;

    public static List<SpecificationViewModel> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            // Anything that is not a JSON object (a bare string, a number, an array) does
            // not describe specifications, so it is ignored rather than guessed at.
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var specifications = new List<SpecificationViewModel>();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (specifications.Count >= MaxRows)
                {
                    break;
                }

                var name = property.Name.Trim();
                var value = ReadValue(property.Value);

                if (name.Length == 0 || value.Length == 0)
                {
                    continue;
                }

                specifications.Add(new SpecificationViewModel
                {
                    Name = name,
                    Value = value.Length > MaxValueLength
                        ? value[..MaxValueLength] + "…"
                        : value,
                });
            }

            return specifications;
        }
        catch (JsonException)
        {
            // Invalid JSON in the column must never take the product page down.
            return [];
        }
    }

    /// <summary>
    /// Values should be strings, but a number, a boolean or a list entered by hand still
    /// renders instead of blowing up. Nested objects have no sensible table cell.
    /// </summary>
    private static string ReadValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString()?.Trim() ?? string.Empty,
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.ToString(),
        JsonValueKind.Array => string.Join(", ", element
            .EnumerateArray()
            .Select(ReadValue)
            .Where(text => text.Length > 0)),
        _ => string.Empty,
    };
}
