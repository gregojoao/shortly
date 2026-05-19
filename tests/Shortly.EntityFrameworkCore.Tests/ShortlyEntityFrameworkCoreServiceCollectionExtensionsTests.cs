using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shortly.Application;
using Shortly.Application.Ports;
using Shortly.EntityFrameworkCore;
using Shortly.Infrastructure.DependencyInjection;
using Shortly.Infrastructure.Storage;

namespace Shortly.EntityFrameworkCore.Tests;

public sealed class ShortlyEntityFrameworkCoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddShortlyEntityFrameworkCoreStore_replaces_the_default_in_memory_store()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseSqlite(connection));
        services.AddShortly(o => o.BaseUrl = new Uri("https://l.example.com"));
        services.AddShortlyEntityFrameworkCoreStore<TestDbContext>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var store = scope.ServiceProvider.GetRequiredService<IShortLinkStore>();
        store.Should().BeOfType<EfCoreShortLinkStore<TestDbContext>>();
        store.Should().NotBeOfType<InMemoryShortLinkStore>();
    }

    [Fact]
    public void ShortlyClient_resolves_with_the_EF_store_inside_a_scope()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseSqlite(connection));
        services.AddShortly(o => o.BaseUrl = new Uri("https://l.example.com"));
        services.AddShortlyEntityFrameworkCoreStore<TestDbContext>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreated();
        var client = scope.ServiceProvider.GetRequiredService<IShortlyClient>();

        client.Should().NotBeNull();
    }
}
