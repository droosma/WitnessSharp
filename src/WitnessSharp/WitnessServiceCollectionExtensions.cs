using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WitnessSharp;

namespace Microsoft.Extensions.DependencyInjection;

public static class WitnessServiceCollectionExtensions
{
    [RequiresUnreferencedCode("Configuration binding uses reflection. Use the Action<WitnessOptions> overload for AOT scenarios.")]
    [RequiresDynamicCode("Configuration binding uses reflection. Use the Action<WitnessOptions> overload for AOT scenarios.")]
    public static IWitnessBuilder AddWitness(this IServiceCollection services, IConfiguration section) =>
        services.AddWitness(section.Bind);

    public static IWitnessBuilder AddWitness(this IServiceCollection services, Action<WitnessOptions> configure)
    {
        // Eagerly evaluate options so resource attributes can be composed at registration time.
        // The same action is also registered with IOptions for runtime consumers (Meter / ActivitySource factories).
        var snapshot = new WitnessOptions();
        configure(snapshot);

        services.Configure(configure);

        services.TryAddSingleton<Meter>(sp =>
            new Meter(sp.GetRequiredService<IOptions<WitnessOptions>>().Value.ServiceName));

        services.TryAddSingleton<ActivitySource>(sp =>
            new ActivitySource(sp.GetRequiredService<IOptions<WitnessOptions>>().Value.ServiceName));

        services.TryAddSingleton(typeof(IWitness<>), typeof(Witness<>));
        services.TryAddSingleton<IWitness>(sp => sp.GetRequiredService<IWitness<object>>());
        services.TryAddSingleton<IWitnessFactory, WitnessFactory>();

        services.AddOpenTelemetry()
            .ConfigureResource(rb => WitnessResource.Apply(rb, snapshot));

        return new WitnessBuilder(services);
    }
}
