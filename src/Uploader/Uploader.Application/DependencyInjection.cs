using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Uploader.Application.Behaviors;
using Uploader.Application.Mapping;
using Uploader.Domain.Services;

namespace Uploader.Application;

/// <summary>Composition of the uploader application layer (CQRS, behaviors, mapper, domain services).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);

        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        // Source DTO -> domain aggregate mapping.
        services.AddSingleton<SourceMapper>();

        // Domain services.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISyncPlanner, FingerprintSyncPlanner>();

        return services;
    }
}
