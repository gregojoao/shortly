namespace Shortly.Domain;

public sealed class ShortLink
{
    public required string Slug { get; init; }

    public required Uri TargetUrl { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public long Hits { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } exp && exp <= now;
}
