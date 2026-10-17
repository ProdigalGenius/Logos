using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SahaNet.Application.Behaviors;
using SahaNet.Application.Identity.Auth;
using SahaNet.Application.Market;
using SahaNet.Application.Options;

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

        AddValidatedOptions<IdentityOptions>(services, IdentityOptions.SectionName);

        AddValidatedOptions<AlertOptions>(services, AlertOptions.SectionName);
        services.AddOptions<RetentionOptions>().BindConfiguration(RetentionOptions.SectionName);

        services.AddOptions<MarketOptions>().BindConfiguration(MarketOptions.SectionName);
        AddValidatedOptions<SahaNet.Application.Payments.PaymentOptions>(services, SahaNet.Application.Payments.PaymentOptions.SectionName);

        services.AddScoped<TokenIssuer>();
        services.AddScoped<IUserNotifier, PushUserNotifier>();
        services.AddOptions<SahaNet.Application.Moderation.SpamOptions>().BindConfiguration(SahaNet.Application.Moderation.SpamOptions.SectionName);
        services.AddSingleton<SahaNet.Application.Moderation.ISpamFilter, SahaNet.Application.Moderation.RuleBasedSpamFilter>();
        services.AddScoped<SahaNet.Application.Moderation.IModerationService, SahaNet.Application.Moderation.ModerationService>();
        services.AddScoped<SahaNet.Application.Modules.IPersonalDataSection, MarketPersonalDataSection>();

        return services;
    }

    private static void AddValidatedOptions<T>(IServiceCollection services, string section)
        where T : class =>
        services.AddOptions<T>()
            .BindConfiguration(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
}
