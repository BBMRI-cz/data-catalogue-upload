using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Uploader.Application.Behaviors;
using Uploader.Domain.Services;

namespace Uploader.Application;

/// <summary>Composition of the uploader application layer (CQRS, behaviors, validators, mapper, domain services).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);

        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        // Scoped, not singleton: AddValidatorsFromAssembly registers validators scoped, and a
        // singleton cannot resolve them - the container throws on the first request that has one.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Application-level request validators (FluentValidation). Auto-registers any
        // AbstractValidator<TCommand> in this assembly; the ValidationBehavior runs them.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Domain services.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISyncPlanner, FingerprintSyncPlanner>();

        return services;
    }
}
