namespace WitnessSharp;

/// <summary>
/// Configuration options for WitnessSharp, used to compose the OpenTelemetry resource and to name
/// the shared <see cref="System.Diagnostics.Metrics.Meter"/> and
/// <see cref="System.Diagnostics.ActivitySource"/>.
/// </summary>
public sealed class WitnessOptions
{
    /// <summary>Gets or sets the logical service name (maps to the <c>service.name</c> resource attribute).</summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>Gets or sets the service namespace (maps to the <c>service.namespace</c> resource attribute).</summary>
    public string? ServiceNamespace { get; set; }

    /// <summary>Gets or sets the service version (maps to the <c>service.version</c> resource attribute).</summary>
    public string? ServiceVersion { get; set; }

    /// <summary>Gets or sets the service instance id (maps to the <c>service.instance.id</c> resource attribute).</summary>
    public string? ServiceInstanceId { get; set; }

    /// <summary>Gets or sets the deployment environment (maps to the <c>deployment.environment</c> resource attribute).</summary>
    public string? DeploymentEnvironment { get; set; }

    /// <summary>Gets additional resource attributes to attach to all telemetry.</summary>
    public IDictionary<string, object> AdditionalResourceAttributes { get; } = new Dictionary<string, object>();

    internal void CopyTo(WitnessOptions other)
    {
        other.ServiceName = ServiceName;
        other.ServiceNamespace = ServiceNamespace;
        other.ServiceVersion = ServiceVersion;
        other.ServiceInstanceId = ServiceInstanceId;
        other.DeploymentEnvironment = DeploymentEnvironment;

        other.AdditionalResourceAttributes.Clear();
        foreach (var attribute in AdditionalResourceAttributes)
        {
            other.AdditionalResourceAttributes[attribute.Key] = attribute.Value;
        }
    }
}
