using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SahaNet.Application.Behaviors;

namespace SahaNet.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Application katmanını kaydeder. Modül assembly'leri de verilirse
    /// onların handler ve validator'ları da taranır.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, params System.Reflection.Assembly[] moduleAssemblies)
    {
        var assemblies = moduleAssemblies.Prepend(typeof(DependencyInjection).Assembly).Distinct().ToArray();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(assemblies);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);

        return services;
    }
}
