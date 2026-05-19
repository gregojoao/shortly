using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Shortly.Application.Ports;
using Shortly.Domain;

namespace Shortly.Infrastructure.Caching;

public sealed class DistributedShortLinkCache : IShortLinkCache
{
    private const string KeyPrefix = "shortly:slug:";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IDistributedCache _cache;

    public DistributedShortLinkCache(IDistributedCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<ShortLink?> GetAsync(string slug, CancellationToken cancellationToken = default)
    {
        var payload = await _cache.GetAsync(BuildKey(slug), cancellationToken).ConfigureAwait(false);
        if (payload is null || payload.Length == 0)
        {
            return null;
        }

        var snapshot = JsonSerializer.Deserialize<ShortLinkSnapshot>(payload, SerializerOptions);
        return snapshot?.ToShortLink();
    }

    public async Task SetAsync(ShortLink shortLink, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shortLink);

        var snapshot = ShortLinkSnapshot.FromShortLink(shortLink);
        var payload = JsonSerializer.SerializeToUtf8Bytes(snapshot, SerializerOptions);

        var entryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        };

        await _cache.SetAsync(BuildKey(shortLink.Slug), payload, entryOptions, cancellationToken).ConfigureAwait(false);
    }

    public Task RemoveAsync(string slug, CancellationToken cancellationToken = default)
        => _cache.RemoveAsync(BuildKey(slug), cancellationToken);

    private static string BuildKey(string slug) => KeyPrefix + slug;

    private sealed class ShortLinkSnapshot
    {
        public string Slug { get; set; } = string.Empty;

        public string TargetUrl { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset? ExpiresAt { get; set; }

        public long Hits { get; set; }

        public Dictionary<string, string>? Metadata { get; set; }

        public static ShortLinkSnapshot FromShortLink(ShortLink link) => new()
        {
            Slug = link.Slug,
            TargetUrl = link.TargetUrl.AbsoluteUri,
            CreatedAt = link.CreatedAt,
            ExpiresAt = link.ExpiresAt,
            Hits = link.Hits,
            Metadata = link.Metadata.Count == 0 ? null : new Dictionary<string, string>(link.Metadata)
        };

        public ShortLink ToShortLink() => new()
        {
            Slug = Slug,
            TargetUrl = new Uri(TargetUrl, UriKind.Absolute),
            CreatedAt = CreatedAt,
            ExpiresAt = ExpiresAt,
            Hits = Hits,
            Metadata = Metadata is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(Metadata, StringComparer.Ordinal)
        };
    }
}
