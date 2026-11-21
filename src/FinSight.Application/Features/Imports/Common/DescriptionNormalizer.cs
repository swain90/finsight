namespace FinSight.Application.Features.Imports.Common;

/// <summary>
/// Reduces a bank transaction description to a stable key so the same merchant
/// produces the same text across statement exports (store numbers, references
/// and punctuation vary between exports).
/// </summary>
public static class DescriptionNormalizer
{
    private const int MaxLength = 200;

    /// <summary>
    /// Normalizes a raw description. Rules, applied in order:
    /// uppercase (invariant), replace non letter/digit/space characters with a space,
    /// drop all-digit tokens of at least 3 characters, collapse whitespace and trim,
    /// cap at 200 characters.
    /// </summary>
    /// <param name="description">The raw description from a statement row.</param>
    /// <returns>The normalized description key.</returns>
    public static string Normalize(string description)
    {
        var upper = description.ToUpperInvariant();

        var replaced = new string(upper.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray());

        var tokens = replaced
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !(token.Length >= 3 && token.All(char.IsDigit)));

        var normalized = string.Join(' ', tokens);

        if (normalized.Length > MaxLength)
        {
            normalized = normalized[..MaxLength].TrimEnd();
        }

        return normalized;
    }
}
