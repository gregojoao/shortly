using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using Shortly.Application.Ports;

namespace Shortly.Mongo;

public static class ShortlyMongoServiceCollectionExtensions
{
    public static IServiceCollection AddShortlyMongoStore(
        this IServiceCollection services,
        Func<IServiceProvider, IMongoDatabase> databaseFactory,
        Action<MongoShortLinkStoreOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(databaseFactory);

        var options = new MongoShortLinkStoreOptions();
        configureOptions?.Invoke(options);
        options.Validate();

        services.RemoveAll<IShortLinkStore>();
        services.AddSingleton(options);
        services.AddSingleton<IShortLinkStore>(sp =>
            new MongoShortLinkStore(databaseFactory(sp), options));

        return services;
    }

    public static IServiceCollection AddShortlyMongoStore(
        this IServiceCollection services,
        string connectionString,
        string databaseName,
        Action<MongoShortLinkStoreOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A MongoDB connection string is required.", nameof(connectionString));
        }
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new ArgumentException("A MongoDB database name is required.", nameof(databaseName));
        }

        services.TryAddSingleton<IMongoClient>(_ => new MongoClient(connectionString));

        return services.AddShortlyMongoStore(
            sp => sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName),
            configureOptions);
    }
}
