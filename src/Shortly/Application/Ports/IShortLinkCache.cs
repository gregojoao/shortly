using Shortly.Domain;

namespace Shortly.Application.Ports;

public interface IShortLinkCache
{
    Task<ShortLink?> GetAsync(string slug, CancellationToken cancellationToken = default);

    Task SetAsync(ShortLink shortLink, TimeSpan ttl, CancellationToken cancellationToken = default);

    Task RemoveAsync(string slug, CancellationToken cancellationToken = default);
}
