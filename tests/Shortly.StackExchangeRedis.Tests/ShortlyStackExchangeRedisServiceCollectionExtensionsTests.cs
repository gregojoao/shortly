using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Shortly.Application.Ports;
using Shortly.Infrastructure.Caching;
using Shortly.Infrastructure.DependencyInjection;
using Shortly.StackExchangeRedis;

namespace Shortly.StackExchangeRedis.Tests;

public sealed class ShortlyStackExchangeRedisServiceCollectionExtensionsTests
{
    [Fact]
    public void AddShortlyStackExchangeRedisCache_registers_DistributedShortLinkCache()
    {
        var services = new ServiceCollection();
        services.AddShortly(o => o.BaseUrl = new Uri("https://l.example.com"));
        services.AddShortlyStackExchangeRedisCache("localhost:6379");

        using var provider = services.BuildServiceProvider();

        var cache = provider.GetRequiredService<IShortLinkCache>();
        cache.Should().BeOfType<DistributedShortLinkCache>();
    }

    [Fact]
    public void AddShortlyStackExchangeRedisCache_registers_an_IDistributedCache_implementation()
    {
        var services = new ServiceCollection();
        services.AddShortly(o => o.BaseUrl = new Uri("https://l.example.com"));
        services.AddShortlyStackExchangeRedisCache("localhost:6379", "myapp:");

        using var provider = services.BuildServiceProvider();

        provider.GetService<IDistributedCache>().Should().NotBeNull();
    }

    [Fact]
    public void Connection_string_overload_rejects_blank_input()
    {
        var services = new ServiceCollection();

        var act = () => services.AddShortlyStackExchangeRedisCache("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Lambda_overload_rejects_null_configuration()
    {
        var services = new ServiceCollection();

        var act = () => services.AddShortlyStackExchangeRedisCache(
            (Action<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions>)null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
