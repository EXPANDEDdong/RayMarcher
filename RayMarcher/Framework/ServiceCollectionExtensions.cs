using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace RayMarcher.Framework;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFrameSystems(
        this IServiceCollection services, Assembly assembly)
    {
        var types = FrameSystemDiscovery.DiscoverTypes(assembly);

        foreach (var type in types)
            services.AddSingleton(type);

        services.AddSingleton(new FrameSystemCatalog(types));
        services.AddSingleton<FrameScheduler>();
        return services;
    }
}