using FluentAssertions;
using Shortly.Domain;
using Shortly.Infrastructure.Storage;

namespace Shortly.Tests.Infrastructure.Storage;

public sealed class InMemoryShortLinkStoreTests
{
    [Fact]
    public async Task SaveAsync_then_FindBySlugAsync_returns_the_saved_link()
    {
        var store = new InMemoryShortLinkStore();
        var link = NewLink("abc1234", "https://example.com/x");

        await store.SaveAsync(link);
        var loaded = await store.FindBySlugAsync("abc1234");

        loaded.Should().NotBeNull();
        loaded!.Slug.Should().Be("abc1234");
        loaded.TargetUrl.AbsoluteUri.Should().Be("https://example.com/x");
    }

    [Fact]
    public async Task FindByTargetAsync_returns_the_link_for_a_known_target()
    {
        var store = new InMemoryShortLinkStore();
        var link = NewLink("abc1234", "https://example.com/x");
        await store.SaveAsync(link);

        var loaded = await store.FindByTargetAsync(new Uri("https://example.com/x"));

        loaded.Should().NotBeNull();
        loaded!.Slug.Should().Be("abc1234");
    }

    [Fact]
    public async Task FindByTargetAsync_returns_null_when_unknown()
    {
        var store = new InMemoryShortLinkStore();
        var loaded = await store.FindByTargetAsync(new Uri("https://nope.example.com/"));
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_removes_the_link_and_target_index()
    {
        var store = new InMemoryShortLinkStore();
        await store.SaveAsync(NewLink("abc1234", "https://example.com/x"));

        var removed = await store.DeleteAsync("abc1234");

        removed.Should().BeTrue();
        (await store.FindBySlugAsync("abc1234")).Should().BeNull();
        (await store.FindByTargetAsync(new Uri("https://example.com/x"))).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_returns_false_for_missing_slug()
    {
        var store = new InMemoryShortLinkStore();
        var removed = await store.DeleteAsync("missing");
        removed.Should().BeFalse();
    }

    [Fact]
    public async Task IncrementHitsAsync_increases_the_counter()
    {
        var store = new InMemoryShortLinkStore();
        await store.SaveAsync(NewLink("abc1234", "https://example.com/x"));

        await store.IncrementHitsAsync("abc1234");
        await store.IncrementHitsAsync("abc1234");

        var loaded = await store.FindBySlugAsync("abc1234");
        loaded!.Hits.Should().Be(2);
    }

    private static ShortLink NewLink(string slug, string target) => new()
    {
        Slug = slug,
        TargetUrl = new Uri(target),
        CreatedAt = DateTimeOffset.UtcNow,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
    };
}
