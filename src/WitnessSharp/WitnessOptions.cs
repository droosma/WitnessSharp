namespace WitnessSharp;

public sealed class WitnessOptions
{
    public string ServiceName { get; set; } = string.Empty;
    public string? ServiceNamespace { get; set; }
    public string? ServiceVersion { get; set; }
    public string? ServiceInstanceId { get; set; }
    public string? DeploymentEnvironment { get; set; }
    public IDictionary<string, object> AdditionalResourceAttributes { get; } = new Dictionary<string, object>();
}
