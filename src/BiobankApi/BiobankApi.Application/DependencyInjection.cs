using BiobankApi.Application.Behaviors;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace BiobankApi.Application;

/// <summary>Composition of the biobank_api application layer (CQRS, behaviors, validators).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Source-generated Mediator registration; handlers run in the request scope.
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);

        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        // Scoped, not singleton: AddValidatorsFromAssembly registers validators scoped, and a
        // singleton cannot resolve them - the container throws on the first request that has one.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Application-level request validators (FluentValidation). Auto-registers any
        // AbstractValidator<TCommand> in this assembly; the ValidationBehavior runs them.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
