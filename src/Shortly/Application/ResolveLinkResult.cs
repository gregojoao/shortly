namespace Shortly.Application;

public sealed class ResolveLinkResult
{
    public required string Slug { get; init; }

    public required Uri TargetUrl { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public bool ServedFromCache { get; init; }
}
