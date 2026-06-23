using BiobankApi.Application.Behaviors;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace BiobankApi.Application;

/// <summary>Composition of the biobank_api application layer (CQRS, behaviors).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Source-generated Mediator registration; handlers run in the request scope.
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);

        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        return services;
    }
}
