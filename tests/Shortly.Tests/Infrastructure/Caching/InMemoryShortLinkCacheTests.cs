using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Shortly.Domain;
using Shortly.Infrastructure.Caching;

namespace Shortly.Tests.Infrastructure.Caching;

public sealed class InMemoryShortLinkCacheTests
{
    [Fact]
    public async Task GetAsync_returns_null_when_slug_is_absent()
    {
        var cache = new InMemoryShortLinkCache(new MemoryCache(new MemoryCacheOptions()));

        var loaded = await cache.GetAsync("missing");

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_then_GetAsync_returns_the_same_link()
    {
        var cache = new InMemoryShortLinkCache(new MemoryCache(new MemoryCacheOptions()));
        var link = NewLink("abc1234", "https://example.com/x");

        await cache.SetAsync(link, TimeSpan.FromMinutes(5));
        var loaded = await cache.GetAsync("abc1234");

        loaded.Should().NotBeNull();
        loaded!.TargetUrl.Should().Be(link.TargetUrl);
    }

    [Fact]
    public async Task RemoveAsync_evicts_the_entry()
    {
        var cache = new InMemoryShortLinkCache(new MemoryCache(new MemoryCacheOptions()));
        var link = NewLink("abc1234", "https://example.com/x");
        await cache.SetAsync(link, TimeSpan.FromMinutes(5));

        await cache.RemoveAsync("abc1234");

        (await cache.GetAsync("abc1234")).Should().BeNull();
    }

    private static ShortLink NewLink(string slug, string target) => new()
    {
        Slug = slug,
        TargetUrl = new Uri(target),
        CreatedAt = DateTimeOffset.UtcNow,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
    };
}
