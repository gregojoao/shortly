using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shortly.Application.Ports;

namespace Shortly.Dapper;

public static class ShortlyDapperServiceCollectionExtensions
{
    public static IServiceCollection AddShortlyDapperStore(
        this IServiceCollection services,
        Func<IServiceProvider, IDbConnection> connectionFactory,
        Action<DapperShortLinkStoreOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionFactory);

        var options = new DapperShortLinkStoreOptions();
        configureOptions?.Invoke(options);
        options.Validate();

        services.RemoveAll<IShortLinkStore>();
        services.AddSingleton(options);
        services.AddScoped<IShortLinkStore>(sp =>
            new DapperShortLinkStore(() => connectionFactory(sp), options));

        return services;
    }
}
