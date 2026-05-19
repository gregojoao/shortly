using Shortly.Domain;

namespace Shortly.Application;

public sealed class ShortlyOptions
{
    public static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(24);
    public static readonly int DefaultSlugLength = 7;
    public static readonly int DefaultMaxSlugGenerationAttempts = 5;

    public static readonly IReadOnlyCollection<string> DefaultAllowedSchemes =
        new[] { Uri.UriSchemeHttp, Uri.UriSchemeHttps };

    public static readonly IReadOnlyCollection<string> DefaultReservedSlugs =
        new[] { "admin", "api", "health", "robots.txt", "favicon.ico" };

    public Uri? BaseUrl { get; set; }

    public int SlugLength { get; set; } = DefaultSlugLength;

    public int MaxSlugGenerationAttempts { get; set; } = DefaultMaxSlugGenerationAttempts;

    public TimeSpan CacheTtl { get; set; } = DefaultCacheTtl;

    public TimeSpan? DefaultLinkTtl { get; set; }

    public bool DeduplicateByTarget { get; set; } = true;

    public bool TrackHits { get; set; } = true;

    public IReadOnlyCollection<string> AllowedSchemes { get; set; } = DefaultAllowedSchemes;

    public IReadOnlyCollection<string> AllowedHosts { get; set; } = Array.Empty<string>();

    public IReadOnlyCollection<string> ReservedSlugs { get; set; } = DefaultReservedSlugs;

    public void Validate()
    {
        if (BaseUrl is null ||
            !BaseUrl.IsAbsoluteUri ||
            (BaseUrl.Scheme != Uri.UriSchemeHttp && BaseUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "Shortly BaseUrl must be an absolute HTTP/HTTPS URL (e.g. https://l.example.com).");
        }

        if (SlugLength < Slug.MinLength || SlugLength > Slug.MaxLength)
        {
            throw new InvalidOperationException(
                $"Shortly SlugLength must be between {Slug.MinLength} and {Slug.MaxLength}.");
        }

        if (MaxSlugGenerationAttempts < 1)
        {
            throw new InvalidOperationException(
                "Shortly MaxSlugGenerationAttempts must be at least 1.");
        }

        if (CacheTtl <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Shortly CacheTtl must be greater than zero.");
        }

        if (DefaultLinkTtl is { } ttl && ttl <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "Shortly DefaultLinkTtl must be greater than zero when specified.");
        }

        if (AllowedSchemes is null || AllowedSchemes.Count == 0)
        {
            throw new InvalidOperationException("Shortly AllowedSchemes must contain at least one entry.");
        }

        AllowedHosts ??= Array.Empty<string>();
        ReservedSlugs ??= Array.Empty<string>();
    }
}
