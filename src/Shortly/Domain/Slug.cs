using System.Text.RegularExpressions;

namespace Shortly.Domain;

public static class Slug
{
    public const int MinLength = 3;
    public const int MaxLength = 64;

    private static readonly Regex AllowedSlugPattern =
        new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsValid(string? slug)
    {
        if (string.IsNullOrEmpty(slug))
        {
            return false;
        }

        if (slug.Length < MinLength || slug.Length > MaxLength)
        {
            return false;
        }

        return AllowedSlugPattern.IsMatch(slug);
    }

    public static string Normalize(string slug)
    {
        ArgumentNullException.ThrowIfNull(slug);
        return slug.Trim();
    }
}
