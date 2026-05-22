using OpenTelemetry.Resources;

namespace WitnessSharp;

internal static class WitnessResource
{
    public static void Apply(ResourceBuilder builder, WitnessOptions options)
    {
        if (!string.IsNullOrEmpty(options.ServiceName))
        {
            // ServiceInstanceId always has a value (option or MachineName fallback),
            // so OTel's autoGenerate path is unreachable; no need to opt out of it.
            builder.AddService(
                serviceName: options.ServiceName,
                serviceNamespace: options.ServiceNamespace,
                serviceVersion: options.ServiceVersion,
                serviceInstanceId: options.ServiceInstanceId ?? Environment.MachineName);
        }

        var deploymentEnvironment = ResolveDeploymentEnvironment(options);
        if (!string.IsNullOrEmpty(deploymentEnvironment))
        {
            builder.AddAttributes(
            [
                new KeyValuePair<string, object>("deployment.environment", deploymentEnvironment),
            ]);
        }

        // Unconditionally pass through — AddAttributes on an empty enumerable is a no-op,
        // and the guard would be an unobservable optimization that mutation testing can't kill.
        builder.AddAttributes(options.AdditionalResourceAttributes);
    }

    private static string? ResolveDeploymentEnvironment(WitnessOptions options) =>
        options.DeploymentEnvironment
        ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
}
