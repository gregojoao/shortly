using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shortly.Application.Ports;

namespace Shortly.EntityFrameworkCore;

public static class ShortlyEntityFrameworkCoreServiceCollectionExtensions
{
    public static IServiceCollection AddShortlyEntityFrameworkCoreStore<TContext>(
        this IServiceCollection services)
        where TContext : DbContext, IShortlyDbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IShortLinkStore>();
        services.AddScoped<IShortLinkStore, EfCoreShortLinkStore<TContext>>();

        return services;
    }
}
