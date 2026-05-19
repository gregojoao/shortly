using Shortly.Application.Ports;
using Shortly.Domain;

namespace Shortly.Infrastructure.Caching;

public sealed class NoOpShortLinkCache : IShortLinkCache
{
    public Task<ShortLink?> GetAsync(string slug, CancellationToken cancellationToken = default)
        => Task.FromResult<ShortLink?>(null);

    public Task SetAsync(ShortLink shortLink, TimeSpan ttl, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemoveAsync(string slug, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
