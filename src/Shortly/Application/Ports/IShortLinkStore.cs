using Shortly.Domain;

namespace Shortly.Application.Ports;

public interface IShortLinkStore
{
    Task<ShortLink?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<ShortLink?> FindByTargetAsync(Uri targetUrl, CancellationToken cancellationToken = default);

    Task SaveAsync(ShortLink shortLink, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string slug, CancellationToken cancellationToken = default);

    Task IncrementHitsAsync(string slug, CancellationToken cancellationToken = default);
}
