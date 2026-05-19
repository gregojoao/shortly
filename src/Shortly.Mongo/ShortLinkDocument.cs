using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Shortly.Mongo;

public sealed class ShortLinkDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Slug { get; set; } = string.Empty;

    [BsonElement("targetUrl")]
    public string TargetUrl { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("expiresAt")]
    [BsonIgnoreIfNull]
    public DateTime? ExpiresAt { get; set; }

    [BsonElement("hits")]
    public long Hits { get; set; }

    [BsonElement("metadata")]
    [BsonIgnoreIfNull]
    public Dictionary<string, string>? Metadata { get; set; }
}
