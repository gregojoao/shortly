using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shortly.Application.Ports;
using Shortly.Infrastructure.Caching;

namespace Shortly.StackExchangeRedis;

public static class ShortlyStackExchangeRedisServiceCollectionExtensions
{
    public static IServiceCollection AddShortlyStackExchangeRedisCache(
        this IServiceCollection services,
        Action<RedisCacheOptions> configureRedis)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureRedis);

        services.AddStackExchangeRedisCache(configureRedis);
        services.RemoveAll<IShortLinkCache>();
        services.AddSingleton<IShortLinkCache, DistributedShortLinkCache>();

        return services;
    }

    public static IServiceCollection AddShortlyStackExchangeRedisCache(
        this IServiceCollection services,
        string connectionString,
        string instanceName = "shortly:")
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A Redis connection string is required.", nameof(connectionString));
        }

        return services.AddShortlyStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            options.InstanceName = instanceName;
        });
    }
}
