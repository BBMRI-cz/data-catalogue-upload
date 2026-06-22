using BiobankApi.Application.Behaviors;
using BiobankApi.Domain.Services;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace BiobankApi.Application;

/// <summary>Composition of the biobank_api application layer (CQRS, behaviors, domain services).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Source-generated Mediator registration; handlers run in the request scope.
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);

        // Pipeline order: logging wraps validation wraps the handler.
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddValidatorsFromAssembly(
            typeof(DependencyInjection).Assembly,
            includeInternalTypes: true);

        // Domain service: pure, stateless field-level cleaning/parsing.
        services.AddSingleton<IBiobankCleaningService, BiobankCleaningService>();

        return services;
    }
}
