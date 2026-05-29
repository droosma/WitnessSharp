using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WitnessSharp;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering WitnessSharp services on an <see cref="IServiceCollection"/>.
/// </summary>
public static class WitnessServiceCollectionExtensions
{
    /// <summary>
    /// Registers WitnessSharp using a configuration section bound to <see cref="WitnessOptions"/>.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="section">The configuration section to bind <see cref="WitnessOptions"/> from.</param>
    /// <returns>An <see cref="IWitnessBuilder"/> for further fluent configuration.</returns>
    [RequiresUnreferencedCode("Configuration binding uses reflection. Use the Action<WitnessOptions> overload for AOT scenarios.")]
    [RequiresDynamicCode("Configuration binding uses reflection. Use the Action<WitnessOptions> overload for AOT scenarios.")]
    public static IWitnessBuilder AddWitness(this IServiceCollection services, IConfiguration section) =>
        services.AddWitness(section.Bind);

    /// <summary>
    /// Registers WitnessSharp using a delegate to configure <see cref="WitnessOptions"/>.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="configure">A delegate used to configure <see cref="WitnessOptions"/>.</param>
    /// <returns>An <see cref="IWitnessBuilder"/> for further fluent configuration.</returns>
    /// <remarks>
    /// The <paramref name="configure"/> delegate is evaluated exactly once. The resolved values
    /// are reused both for the OpenTelemetry resource (composed at registration time) and for the
    /// runtime <see cref="IOptions{TOptions}"/> consumers, so the two can never diverge. If
    /// <see cref="WitnessOptions.ServiceName"/> is left empty it is defaulted to the entry assembly
    /// name (falling back to <c>"unknown_service"</c>) to avoid emitting empty-named instruments and
    /// a resource without a <c>service.name</c>.
    /// </remarks>
    public static IWitnessBuilder AddWitness(this IServiceCollection services, Action<WitnessOptions> configure)
    {
        // Stryker disable once Statement : explicit fail-fast guard. Removing it is an equivalent mutant
        // because the first downstream use (services.Configure below) throws an identical
        // ArgumentNullException with the same "services" parameter name.
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Evaluate the caller's delegate exactly once. The resolved snapshot is reused for both the
        // resource (composed eagerly below) and the runtime IOptions instance (copied in), which
        // guarantees the resource attributes and the Meter / ActivitySource names stay in sync.
        var options = new WitnessOptions();
        configure(options);
        EnsureServiceName(options);

        services.Configure<WitnessOptions>(options.CopyTo);

        services.TryAddSingleton<Meter>(sp =>
            new Meter(sp.GetRequiredService<IOptions<WitnessOptions>>().Value.ServiceName));

        services.TryAddSingleton<ActivitySource>(sp =>
            new ActivitySource(sp.GetRequiredService<IOptions<WitnessOptions>>().Value.ServiceName));

        services.TryAddSingleton(typeof(IWitness<>), typeof(Witness<>));
        services.TryAddSingleton<IWitness>(sp => sp.GetRequiredService<IWitness<object>>());
        services.TryAddSingleton<IWitnessFactory, WitnessFactory>();

        services.AddOpenTelemetry()
            .ConfigureResource(rb => WitnessResource.Apply(rb, options));

        return new WitnessBuilder(services);
    }

    private static void EnsureServiceName(WitnessOptions options)
    {
        if (string.IsNullOrEmpty(options.ServiceName))
        {
            // Stryker disable once String : the "unknown_service" fallback is only reached when
            // Assembly.GetEntryAssembly() is null, which never happens under a managed test host,
            // so mutating the literal produces an equivalent (unkillable) mutant.
            options.ServiceName = Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown_service";
        }
    }
}
