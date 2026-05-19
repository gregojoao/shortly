using LiteDB;

namespace Shortly.LiteDb;

public sealed class ShortLinkDocument
{
    [BsonId]
    public string Slug { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = string.Empty;

    public long CreatedAtTicks { get; set; }

    public long? ExpiresAtTicks { get; set; }

    public long Hits { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }
}
