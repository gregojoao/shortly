using FluentAssertions;
using LiteDB;
using Shortly.Domain;
using Shortly.LiteDb;

namespace Shortly.LiteDb.Tests;

public sealed class LiteDbShortLinkStoreTests : IDisposable
{
    private readonly MemoryStream _stream;
    private readonly LiteDatabase _database;
    private readonly LiteDbShortLinkStore _store;

    public LiteDbShortLinkStoreTests()
    {
        _stream = new MemoryStream();
        _database = new LiteDatabase(_stream);
        _store = new LiteDbShortLinkStore(_database);
    }

    public void Dispose()
    {
        _database.Dispose();
        _stream.Dispose();
    }

    [Fact]
    public async Task SaveAsync_then_FindBySlugAsync_returns_the_saved_link()
    {
        var link = NewLink("abc1234", "https://example.com/x", metadata: new()
        {
            ["source"] = "litedb-test"
        });

        await _store.SaveAsync(link);
        var loaded = await _store.FindBySlugAsync("abc1234");

        loaded.Should().NotBeNull();
        loaded!.Slug.Should().Be("abc1234");
        loaded.TargetUrl.AbsoluteUri.Should().Be("https://example.com/x");
        loaded.Metadata.Should().ContainKey("source").WhoseValue.Should().Be("litedb-test");
    }

    [Fact]
    public async Task FindByTargetAsync_returns_the_link_for_a_known_target()
    {
        await _store.SaveAsync(NewLink("abc1234", "https://example.com/x"));

        var loaded = await _store.FindByTargetAsync(new Uri("https://example.com/x"));

        loaded.Should().NotBeNull();
        loaded!.Slug.Should().Be("abc1234");
    }

    [Fact]
    public async Task FindByTargetAsync_returns_null_when_unknown()
    {
        var loaded = await _store.FindByTargetAsync(new Uri("https://nope.example.com"));
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_updates_an_existing_record()
    {
        await _store.SaveAsync(NewLink("abc1234", "https://example.com/old"));
        await _store.SaveAsync(NewLink("abc1234", "https://example.com/new"));

        var loaded = await _store.FindBySlugAsync("abc1234");

        loaded!.TargetUrl.AbsoluteUri.Should().Be("https://example.com/new");
    }

    [Fact]
    public async Task DeleteAsync_removes_the_record()
    {
        await _store.SaveAsync(NewLink("abc1234", "https://example.com/x"));

        var removed = await _store.DeleteAsync("abc1234");

        removed.Should().BeTrue();
        (await _store.FindBySlugAsync("abc1234")).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_returns_false_for_unknown_slug()
    {
        var removed = await _store.DeleteAsync("missing");
        removed.Should().BeFalse();
    }

    [Fact]
    public async Task IncrementHitsAsync_bumps_the_counter()
    {
        await _store.SaveAsync(NewLink("abc1234", "https://example.com/x"));

        await _store.IncrementHitsAsync("abc1234");
        await _store.IncrementHitsAsync("abc1234");
        await _store.IncrementHitsAsync("abc1234");

        (await _store.FindBySlugAsync("abc1234"))!.Hits.Should().Be(3);
    }

    [Fact]
    public async Task IncrementHitsAsync_is_safe_for_unknown_slug()
    {
        var act = async () => await _store.IncrementHitsAsync("missing");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CreatedAt_round_trips_as_UTC()
    {
        var when = new DateTimeOffset(2026, 5, 19, 10, 30, 0, TimeSpan.Zero);
        var link = new ShortLink
        {
            Slug = "abc1234",
            TargetUrl = new Uri("https://example.com/x"),
            CreatedAt = when,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        };

        await _store.SaveAsync(link);
        var loaded = await _store.FindBySlugAsync("abc1234");

        loaded!.CreatedAt.UtcDateTime.Should().Be(when.UtcDateTime);
    }

    private static ShortLink NewLink(
        string slug,
        string target,
        Dictionary<string, string>? metadata = null) => new()
    {
        Slug = slug,
        TargetUrl = new Uri(target),
        CreatedAt = DateTimeOffset.UtcNow,
        Metadata = metadata ?? new Dictionary<string, string>(StringComparer.Ordinal)
    };
}
