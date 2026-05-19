using System.Data;
using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Shortly.Dapper;
using Shortly.Domain;

namespace Shortly.Dapper.Tests;

public sealed class DapperShortLinkStoreTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private string _connectionString = null!;
    private DapperShortLinkStore _store = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"shortly-dapper-{Guid.NewGuid():N}.db");
        _connectionString = $"DataSource={_dbPath}";

        await EnsureSchemaAsync(_connectionString, DapperShortLinkStoreOptions.DefaultTableName);

        _store = new DapperShortLinkStore(() => new SqliteConnection(_connectionString));
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* best effort */ }
        }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SaveAsync_then_FindBySlugAsync_returns_the_saved_link()
    {
        var link = NewLink("abc1234", "https://example.com/x", metadata: new()
        {
            ["source"] = "dapper-test"
        });

        await _store.SaveAsync(link);
        var loaded = await _store.FindBySlugAsync("abc1234");

        loaded.Should().NotBeNull();
        loaded!.Slug.Should().Be("abc1234");
        loaded.TargetUrl.AbsoluteUri.Should().Be("https://example.com/x");
        loaded.Metadata.Should().ContainKey("source").WhoseValue.Should().Be("dapper-test");
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
    public async Task DeleteAsync_returns_false_for_unknown_slug()
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

        (await _store.FindBySlugAsync("abc1234"))!.Hits.Should().Be(2);
    }

    [Fact]
    public void Constructor_rejects_invalid_table_name()
    {
        var act = () => new DapperShortLinkStore(
            () => new SqliteConnection("DataSource=:memory:"),
            new DapperShortLinkStoreOptions { TableName = "bad name; DROP TABLE" });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TableName*");
    }

    [Fact]
    public async Task Custom_table_name_is_honored()
    {
        const string table = "PromoLinks";

        var customDbPath = Path.Combine(Path.GetTempPath(), $"shortly-dapper-{Guid.NewGuid():N}.db");
        var customConnectionString = $"DataSource={customDbPath}";

        try
        {
            await EnsureSchemaAsync(customConnectionString, table);

            var store = new DapperShortLinkStore(
                () => new SqliteConnection(customConnectionString),
                new DapperShortLinkStoreOptions { TableName = table });

            await store.SaveAsync(NewLink("custom1", "https://example.com/c"));

            await using var connection = new SqliteConnection(customConnectionString);
            var count = await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {table}");
            count.Should().Be(1);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(customDbPath))
            {
                try { File.Delete(customDbPath); } catch { /* best effort */ }
            }
        }
    }

    private static async Task EnsureSchemaAsync(string connectionString, string tableName)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var statements = DapperShortLinkSql.Sqlite(tableName)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var statement in statements)
        {
            await connection.ExecuteAsync(statement);
        }
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
