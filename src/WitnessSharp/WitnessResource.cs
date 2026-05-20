using OpenTelemetry.Resources;

namespace WitnessSharp;

internal static class WitnessResource
{
    public static void Apply(ResourceBuilder builder, WitnessOptions options)
    {
        if (!string.IsNullOrEmpty(options.ServiceName))
        {
            builder.AddService(
                serviceName: options.ServiceName,
                serviceNamespace: options.ServiceNamespace,
                serviceVersion: options.ServiceVersion,
                autoGenerateServiceInstanceId: false,
                serviceInstanceId: options.ServiceInstanceId ?? Environment.MachineName);
        }

        var deploymentEnvironment = ResolveDeploymentEnvironment(options);
        if (!string.IsNullOrEmpty(deploymentEnvironment))
        {
            builder.AddAttributes(new[]
            {
                new KeyValuePair<string, object>("deployment.environment", deploymentEnvironment),
            });
        }

        if (options.AdditionalResourceAttributes.Count > 0)
        {
            builder.AddAttributes(options.AdditionalResourceAttributes);
        }
    }

    private static string? ResolveDeploymentEnvironment(WitnessOptions options) =>
        options.DeploymentEnvironment
        ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
}
