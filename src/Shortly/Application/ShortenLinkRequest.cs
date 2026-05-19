namespace Shortly.Application;

public sealed class ShortenLinkRequest
{
    public required Uri TargetUrl { get; init; }

    public string? CustomSlug { get; init; }

    public TimeSpan? Ttl { get; init; }

    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
