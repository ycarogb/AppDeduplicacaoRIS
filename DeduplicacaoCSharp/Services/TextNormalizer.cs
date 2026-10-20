using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DeduplicacaoCSharp.Services;

public static class TextNormalizer
{
    public static string NormalizeTitle(string? text)
    {
        var decomposed = (text ?? "").Normalize(NormalizationForm.FormD);
        var result = new StringBuilder();

        foreach (var character in decomposed)
        {
            // Mesma faixa de diacríticos usada na versão JavaScript.
            if (character is >= '\u0300' and <= '\u036f')
            {
                continue;
            }

            result.Append(char.ToLower(character, CultureInfo.InvariantCulture));
        }

        return Regex.Replace(result.ToString(), @"[^\p{L}\p{N}]+", " ").Trim();
    }

    public static string NormalizeDoi(string? text)
    {
        var value = (text ?? "").Trim().ToLowerInvariant();

        return Regex.Replace(
            value,
            @"^(?:https?://(?:dx\.)?doi\.org/|doi:\s*)",
            "").Trim();
    }
}
