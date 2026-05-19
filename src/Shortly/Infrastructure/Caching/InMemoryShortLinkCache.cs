using Microsoft.Extensions.Caching.Memory;
using Shortly.Application.Ports;
using Shortly.Domain;

namespace Shortly.Infrastructure.Caching;

public sealed class InMemoryShortLinkCache : IShortLinkCache
{
    private const string KeyPrefix = "shortly:slug:";

    private readonly IMemoryCache _cache;

    public InMemoryShortLinkCache(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public Task<ShortLink?> GetAsync(string slug, CancellationToken cancellationToken = default)
    {
        _cache.TryGetValue(BuildKey(slug), out ShortLink? link);
        return Task.FromResult(link);
    }

    public Task SetAsync(ShortLink shortLink, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shortLink);
        _cache.Set(BuildKey(shortLink.Slug), shortLink, ttl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string slug, CancellationToken cancellationToken = default)
    {
        _cache.Remove(BuildKey(slug));
        return Task.CompletedTask;
    }

    private static string BuildKey(string slug) => KeyPrefix + slug;
}
