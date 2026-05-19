namespace Shortly.Application;

public sealed class ShortenLinkResult
{
    public required string Slug { get; init; }

    public required Uri ShortUrl { get; init; }

    public required Uri TargetUrl { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public bool AlreadyExisted { get; init; }
}
