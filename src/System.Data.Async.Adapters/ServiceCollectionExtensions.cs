using System.Data.Common;

using Microsoft.Extensions.DependencyInjection;

namespace System.Data.Async.Adapters;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAsyncData(this IServiceCollection services, DbProviderFactory providerFactory)
    {
        services.AddSingleton<IAsyncDbProviderFactory>(new AdapterDbProviderFactory(providerFactory));
        return services;
    }

    public static IServiceCollection AddAsyncData(this IServiceCollection services, IAsyncDbProviderFactory providerFactory)
    {
        services.AddSingleton(providerFactory);
        return services;
    }
}
