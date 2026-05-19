namespace Shortly.EntityFrameworkCore;

public sealed class ShortLinkRecord
{
    public string Slug { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public long Hits { get; set; }

    public string? MetadataJson { get; set; }
}
