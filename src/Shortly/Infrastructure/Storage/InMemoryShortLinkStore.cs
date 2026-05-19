using System.Collections.Concurrent;
using Shortly.Application.Ports;
using Shortly.Domain;

namespace Shortly.Infrastructure.Storage;

public sealed class InMemoryShortLinkStore : IShortLinkStore
{
    private readonly ConcurrentDictionary<string, ShortLink> _bySlug =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, string> _slugByTarget =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<ShortLink?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        _bySlug.TryGetValue(slug, out var link);
        return Task.FromResult<ShortLink?>(link);
    }

    public Task<ShortLink?> FindByTargetAsync(Uri targetUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetUrl);
        if (_slugByTarget.TryGetValue(targetUrl.AbsoluteUri, out var slug) &&
            _bySlug.TryGetValue(slug, out var link))
        {
            return Task.FromResult<ShortLink?>(link);
        }

        return Task.FromResult<ShortLink?>(null);
    }

    public Task SaveAsync(ShortLink shortLink, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shortLink);
        _bySlug[shortLink.Slug] = shortLink;
        _slugByTarget[shortLink.TargetUrl.AbsoluteUri] = shortLink.Slug;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (_bySlug.TryRemove(slug, out var link))
        {
            _slugByTarget.TryRemove(
                new KeyValuePair<string, string>(link.TargetUrl.AbsoluteUri, slug));
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task IncrementHitsAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (!_bySlug.TryGetValue(slug, out var current))
        {
            return Task.CompletedTask;
        }

        var updated = new ShortLink
        {
            Slug = current.Slug,
            TargetUrl = current.TargetUrl,
            CreatedAt = current.CreatedAt,
            ExpiresAt = current.ExpiresAt,
            Hits = current.Hits + 1,
            Metadata = current.Metadata
        };

        _bySlug.TryUpdate(slug, updated, current);
        return Task.CompletedTask;
    }
}
