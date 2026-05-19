using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Shortly.Mongo;

namespace Shortly.Mongo.Tests;

public sealed class MongoShortLinkStoreDocumentTests
{
    [Fact]
    public void ShortLinkDocument_uses_Slug_as_the_BSON_id()
    {
        var classMap = BsonClassMap.LookupClassMap(typeof(ShortLinkDocument));

        classMap.IdMemberMap.Should().NotBeNull();
        classMap.IdMemberMap!.MemberName.Should().Be(nameof(ShortLinkDocument.Slug));
    }

    [Fact]
    public void ShortLinkDocument_serializes_and_deserializes_round_trip()
    {
        var document = new ShortLinkDocument
        {
            Slug = "abc1234",
            TargetUrl = "https://example.com/promo",
            CreatedAt = new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
            ExpiresAt = new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc),
            Hits = 7,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["source"] = "telegram",
                ["campaign"] = "bf-2026"
            }
        };

        var bson = document.ToBsonDocument();
        var roundTrip = BsonSerializer.Deserialize<ShortLinkDocument>(bson);

        roundTrip.Slug.Should().Be("abc1234");
        roundTrip.TargetUrl.Should().Be("https://example.com/promo");
        roundTrip.Hits.Should().Be(7);
        roundTrip.Metadata.Should().ContainKey("source").WhoseValue.Should().Be("telegram");
        roundTrip.Metadata.Should().ContainKey("campaign").WhoseValue.Should().Be("bf-2026");
    }

    [Fact]
    public void ShortLinkDocument_omits_null_optional_fields()
    {
        var document = new ShortLinkDocument
        {
            Slug = "abc1234",
            TargetUrl = "https://example.com/promo",
            CreatedAt = DateTime.UtcNow,
            Hits = 0
        };

        var bson = document.ToBsonDocument();

        bson.Contains("expiresAt").Should().BeFalse();
        bson.Contains("metadata").Should().BeFalse();
    }
}
