using LiteDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shortly.Application.Ports;

namespace Shortly.LiteDb;

public static class ShortlyLiteDbServiceCollectionExtensions
{
    public static IServiceCollection AddShortlyLiteDbStore(
        this IServiceCollection services,
        Func<IServiceProvider, ILiteDatabase> databaseFactory,
        Action<LiteDbShortLinkStoreOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(databaseFactory);

        var options = new LiteDbShortLinkStoreOptions();
        configureOptions?.Invoke(options);
        options.Validate();

        services.RemoveAll<IShortLinkStore>();
        services.AddSingleton(options);
        services.AddSingleton<IShortLinkStore>(sp =>
            new LiteDbShortLinkStore(databaseFactory(sp), options));

        return services;
    }

    public static IServiceCollection AddShortlyLiteDbStore(
        this IServiceCollection services,
        string connectionString,
        Action<LiteDbShortLinkStoreOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A LiteDB connection string is required.", nameof(connectionString));
        }

        services.TryAddSingleton<ILiteDatabase>(_ => new LiteDatabase(connectionString));
        return services.AddShortlyLiteDbStore(
            sp => sp.GetRequiredService<ILiteDatabase>(),
            configureOptions);
    }
}
