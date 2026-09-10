using System.Globalization;
using System.Text;

namespace ElectronicStore.Areas.Admin;

/// <summary>
/// Turns admin input into the slug shape agreed in docs/database-schema.md § 1:
/// lowercase, no diacritics, words separated by "-".
/// </summary>
public static class SlugHelper
{
    /// <summary>
    /// Normalizes a slug typed by the admin. Returns an empty string when nothing usable
    /// is left, so the caller can let <c>[Required]</c> report the problem.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // "Ổ cứng SSD" -> "O cung SSD": split the accents off and drop the marks.
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (c is 'đ')
            {
                builder.Append('d');
            }
            else if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (c is ' ' or '-' or '_' or '.' or '/')
            {
                builder.Append('-');
            }
        }

        // Collapse the runs of dashes the loop above can produce and trim the edges.
        var raw = builder.ToString().Normalize(NormalizationForm.FormC);
        var parts = raw.Split('-', StringSplitOptions.RemoveEmptyEntries);

        return string.Join('-', parts);
    }
}
