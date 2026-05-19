using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shortly.Application;
using Shortly.Application.Ports;
using Shortly.Infrastructure.Caching;
using Shortly.Infrastructure.Slug;
using Shortly.Infrastructure.Storage;

namespace Shortly.Infrastructure.DependencyInjection;

public static class ShortlyServiceCollectionExtensions
{
    public const string DefaultConfigurationSectionName = "Shortly";

    public static IServiceCollection AddShortly(
        this IServiceCollection services,
        Action<ShortlyOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddOptions<ShortlyOptions>()
            .Configure(configureOptions)
            .Validate(AreOptionsValid, "Shortly options are invalid.")
            .ValidateOnStart();

        return services.AddShortlyCore();
    }

    public static IServiceCollection AddShortly(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = DefaultConfigurationSectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(sectionName);
        services.AddOptions<ShortlyOptions>()
            .Bind(section)
            .Validate(AreOptionsValid, $"Configuration section '{sectionName}' contains invalid Shortly options.")
            .ValidateOnStart();

        return services.AddShortlyCore();
    }

    private static IServiceCollection AddShortlyCore(this IServiceCollection services)
    {
        services.AddMemoryCache();

        services.TryAddSingleton<ISlugGenerator, Base62SlugGenerator>();
        services.TryAddSingleton<IShortLinkStore, InMemoryShortLinkStore>();
        services.TryAddSingleton<IShortLinkCache, InMemoryShortLinkCache>();
        services.TryAddSingleton(TimeProvider.System);

        services.TryAddTransient<IShortlyClient, ShortlyClient>();

        return services;
    }

    private static bool AreOptionsValid(ShortlyOptions options)
    {
        try
        {
            options.Validate();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
