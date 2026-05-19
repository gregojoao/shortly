using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shortly.Domain;
using Shortly.EntityFrameworkCore;

namespace Shortly.EntityFrameworkCore.Tests;

public sealed class EfCoreShortLinkStoreTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private TestDbContext _context = null!;
    private EfCoreShortLinkStore<TestDbContext> _store = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new TestDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _store = new EfCoreShortLinkStore<TestDbContext>(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task SaveAsync_then_FindBySlugAsync_returns_the_saved_link()
    {
        var link = NewLink("abc1234", "https://example.com/x", metadata: new()
        {
            ["source"] = "tests",
            ["campaign"] = "ef-core"
        });

        await _store.SaveAsync(link);
        var loaded = await _store.FindBySlugAsync("abc1234");

        loaded.Should().NotBeNull();
        loaded!.Slug.Should().Be("abc1234");
        loaded.TargetUrl.AbsoluteUri.Should().Be("https://example.com/x");
        loaded.Metadata.Should().ContainKey("source").WhoseValue.Should().Be("tests");
        loaded.Metadata.Should().ContainKey("campaign").WhoseValue.Should().Be("ef-core");
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
    public async Task DeleteAsync_returns_false_for_an_unknown_slug()
    {
        var removed = await _store.DeleteAsync("missing");
        removed.Should().BeFalse();
    }

    [Fact]
    public async Task IncrementHitsAsync_atomically_bumps_the_counter()
    {
        await _store.SaveAsync(NewLink("abc1234", "https://example.com/x"));

        await _store.IncrementHitsAsync("abc1234");
        await _store.IncrementHitsAsync("abc1234");
        await _store.IncrementHitsAsync("abc1234");

        (await _store.FindBySlugAsync("abc1234"))!.Hits.Should().Be(3);
    }

    [Fact]
    public async Task FindBySlugAsync_returns_null_when_absent()
    {
        var loaded = await _store.FindBySlugAsync("missing");
        loaded.Should().BeNull();
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
