using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Uploader.Application.Behaviors;
using Uploader.Application.Mapping;
using Uploader.Domain.Services;

namespace Uploader.Application;

/// <summary>Composition of the uploader application layer (CQRS, behaviors, validators, mapper, domain services).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);

        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Application-level request validators (FluentValidation). Auto-registers any
        // AbstractValidator<TCommand> in this assembly; the ValidationBehavior runs them.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Source DTO -> domain aggregate mapping.
        services.AddSingleton<SourceMapper>();

        // Domain services.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISyncPlanner, FingerprintSyncPlanner>();

        return services;
    }
}
