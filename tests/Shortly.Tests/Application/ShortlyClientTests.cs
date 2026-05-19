using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Shortly.Application;
using Shortly.Application.Ports;
using Shortly.Exceptions;
using Shortly.Infrastructure.Caching;
using Shortly.Infrastructure.Slug;
using Shortly.Infrastructure.Storage;

namespace Shortly.Tests.Application;

public sealed class ShortlyClientTests
{
    [Fact]
    public async Task ShortenAsync_generates_a_slug_and_builds_short_url_from_BaseUrl()
    {
        var (client, _) = BuildClient();

        var result = await client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/promo/123")
        });

        result.Slug.Should().NotBeNullOrEmpty();
        result.ShortUrl.AbsoluteUri.Should().StartWith("https://l.example.com/");
        result.ShortUrl.AbsoluteUri.Should().EndWith(result.Slug);
        result.TargetUrl.AbsoluteUri.Should().Be("https://example.com/promo/123");
        result.AlreadyExisted.Should().BeFalse();
        result.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task ShortenAsync_returns_the_existing_link_when_deduplication_is_enabled()
    {
        var (client, _) = BuildClient();
        var first = await client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/promo/123")
        });

        var second = await client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/promo/123")
        });

        second.Slug.Should().Be(first.Slug);
        second.AlreadyExisted.Should().BeTrue();
    }

    [Fact]
    public async Task ShortenAsync_creates_a_new_link_when_deduplication_is_disabled()
    {
        var (client, _) = BuildClient(o => o.DeduplicateByTarget = false);
        var first = await client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/promo/123")
        });

        var second = await client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/promo/123")
        });

        second.Slug.Should().NotBe(first.Slug);
        second.AlreadyExisted.Should().BeFalse();
    }

    [Fact]
    public async Task ShortenAsync_honors_the_custom_slug_when_available()
    {
        var (client, _) = BuildClient();

        var result = await client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/promo/123"),
            CustomSlug = "promo-bf"
        });

        result.Slug.Should().Be("promo-bf");
        result.ShortUrl.AbsoluteUri.Should().Be("https://l.example.com/promo-bf");
    }

    [Fact]
    public async Task ShortenAsync_throws_when_the_custom_slug_is_already_taken_by_another_target()
    {
        var (client, _) = BuildClient();
        await client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/a"),
            CustomSlug = "promo-bf"
        });

        var act = () => client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/b"),
            CustomSlug = "promo-bf"
        });

        await act.Should().ThrowAsync<ShortLinkConflictException>();
    }

    [Fact]
    public async Task ShortenAsync_throws_when_the_custom_slug_is_reserved()
    {
        var (client, _) = BuildClient();

        var act = () => client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/a"),
            CustomSlug = "admin"
        });

        await act.Should().ThrowAsync<ShortlyValidationException>()
            .WithMessage("*reserved*");
    }

    [Fact]
    public async Task ShortenAsync_throws_when_the_target_scheme_is_not_allowed()
    {
        var (client, _) = BuildClient();

        var act = () => client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("ftp://example.com/file")
        });

        await act.Should().ThrowAsync<ShortlyValidationException>();
    }

    [Fact]
    public async Task ShortenAsync_respects_AllowedHosts_when_configured()
    {
        var (client, _) = BuildClient(o => o.AllowedHosts = new[] { "shopee.com.br", "*.shopee.com.br" });

        var ok = await client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://promo.shopee.com.br/abc")
        });
        ok.Slug.Should().NotBeNullOrEmpty();

        var act = () => client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://evil.com/abc")
        });
        await act.Should().ThrowAsync<ShortlyValidationException>();
    }

    [Fact]
    public async Task ShortenAsync_sets_ExpiresAt_when_a_request_Ttl_is_provided()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
        var (client, _) = BuildClient(timeProvider: fakeTime);

        var result = await client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/promo/123"),
            Ttl = TimeSpan.FromDays(7)
        });

        result.ExpiresAt.Should().Be(new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task ResolveAsync_returns_the_target_for_a_known_slug()
    {
        var (client, _) = BuildClient();
        var created = await client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/promo/123")
        });

        var resolved = await client.ResolveAsync(created.Slug);

        resolved.Should().NotBeNull();
        resolved!.TargetUrl.AbsoluteUri.Should().Be("https://example.com/promo/123");
        resolved.Slug.Should().Be(created.Slug);
    }

    [Fact]
    public async Task ResolveAsync_serves_a_second_call_from_cache()
    {
        var (client, _) = BuildClient();
        var created = await client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/promo/123")
        });

        var first = await client.ResolveAsync(created.Slug);
        var second = await client.ResolveAsync(created.Slug);

        first!.ServedFromCache.Should().BeTrue("set during Shorten populated the cache");
        second!.ServedFromCache.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveAsync_returns_null_for_an_unknown_slug()
    {
        var (client, _) = BuildClient();
        var resolved = await client.ResolveAsync("absent1");
        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_evicts_expired_links_and_returns_null()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
        var (client, store) = BuildClient(timeProvider: fakeTime);

        var created = await client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/promo/123"),
            Ttl = TimeSpan.FromMinutes(5)
        });

        fakeTime.Advance(TimeSpan.FromHours(1));
        var resolved = await client.ResolveAsync(created.Slug);

        resolved.Should().BeNull();
        (await store.FindBySlugAsync(created.Slug)).Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_increments_hits_when_TrackHits_is_enabled()
    {
        var (client, store) = BuildClient();
        var created = await client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/promo/123")
        });

        await client.ResolveAsync(created.Slug);
        await client.ResolveAsync(created.Slug);

        (await store.FindBySlugAsync(created.Slug))!.Hits.Should().Be(2);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_link_from_store_and_cache()
    {
        var (client, store) = BuildClient();
        var created = await client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/promo/123")
        });

        var removed = await client.DeleteAsync(created.Slug);

        removed.Should().BeTrue();
        (await store.FindBySlugAsync(created.Slug)).Should().BeNull();
        (await client.ResolveAsync(created.Slug)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_returns_false_when_the_slug_was_not_present()
    {
        var (client, _) = BuildClient();
        var removed = await client.DeleteAsync("missing");
        removed.Should().BeFalse();
    }

    [Fact]
    public async Task ShortenAsync_throws_when_slug_generation_keeps_colliding()
    {
        var fixedSlug = new FixedSlugGenerator("collide");
        var (client, _) = BuildClient(slugGenerator: fixedSlug, configure: o =>
        {
            o.DeduplicateByTarget = false;
            o.MaxSlugGenerationAttempts = 3;
        });

        await client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/a")
        });

        var act = () => client.ShortenAsync(new ShortenLinkRequest
        {
            TargetUrl = new Uri("https://example.com/b")
        });

        await act.Should().ThrowAsync<ShortlyException>()
            .WithMessage("*unique slug*");
    }

    private static (IShortlyClient Client, IShortLinkStore Store) BuildClient(
        Action<ShortlyOptions>? configure = null,
        ISlugGenerator? slugGenerator = null,
        TimeProvider? timeProvider = null)
    {
        var options = new ShortlyOptions { BaseUrl = new Uri("https://l.example.com") };
        configure?.Invoke(options);

        var store = new InMemoryShortLinkStore();
        var cache = new InMemoryShortLinkCache(new MemoryCache(new MemoryCacheOptions()));
        var generator = slugGenerator ?? new Base62SlugGenerator();

        var client = new ShortlyClient(
            store,
            cache,
            generator,
            Options.Create(options),
            timeProvider);

        return (client, store);
    }

    private sealed class FixedSlugGenerator : ISlugGenerator
    {
        private readonly string _slug;

        public FixedSlugGenerator(string slug) => _slug = slug;

        public string Generate(int length) => _slug;
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public FakeTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}
