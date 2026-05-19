namespace Shortly.Application;

public interface IShortlyClient
{
    Task<ShortenLinkResult> ShortenAsync(
        ShortenLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<ResolveLinkResult?> ResolveAsync(
        string slug,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string slug,
        CancellationToken cancellationToken = default);
}
