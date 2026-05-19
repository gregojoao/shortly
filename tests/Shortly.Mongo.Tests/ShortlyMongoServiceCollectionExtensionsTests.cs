using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Shortly.Application.Ports;
using Shortly.Infrastructure.DependencyInjection;
using Shortly.Infrastructure.Storage;
using Shortly.Mongo;

namespace Shortly.Mongo.Tests;

public sealed class ShortlyMongoServiceCollectionExtensionsTests
{
    [Fact]
    public void AddShortlyMongoStore_validates_options()
    {
        var services = new ServiceCollection();
        services.AddShortly(o => o.BaseUrl = new Uri("https://l.example.com"));

        var act = () => services.AddShortlyMongoStore(
            _ => null!,
            o => o.CollectionName = string.Empty);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CollectionName*");
    }

    [Fact]
    public void Connection_string_overload_rejects_blank_input()
    {
        var services = new ServiceCollection();

        var act = () => services.AddShortlyMongoStore(connectionString: "   ", databaseName: "x");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Connection_string_overload_rejects_blank_database_name()
    {
        var services = new ServiceCollection();

        var act = () => services.AddShortlyMongoStore(
            connectionString: "mongodb://localhost:27017",
            databaseName: "   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddShortlyMongoStore_removes_the_default_InMemory_registration()
    {
        var services = new ServiceCollection();
        services.AddShortly(o => o.BaseUrl = new Uri("https://l.example.com"));

        services.Should().Contain(d =>
            d.ServiceType == typeof(IShortLinkStore) &&
            d.ImplementationType == typeof(InMemoryShortLinkStore));

        services.AddShortlyMongoStore(
            connectionString: "mongodb://localhost:27017",
            databaseName: "shortly");

        services.Should().NotContain(d =>
            d.ServiceType == typeof(IShortLinkStore) &&
            d.ImplementationType == typeof(InMemoryShortLinkStore));

        services.Should().Contain(d => d.ServiceType == typeof(IShortLinkStore));
    }
}
