namespace WitnessSharp.Tests;

public class WitnessOptionsTests
{
    [Fact]
    public void ServiceName_defaults_to_empty_string()
    {
        var options = new WitnessOptions();

        Assert.Equal(string.Empty, options.ServiceName);
    }

    [Fact]
    public void Nullable_fields_default_to_null()
    {
        var options = new WitnessOptions();

        Assert.Null(options.ServiceNamespace);
        Assert.Null(options.ServiceVersion);
        Assert.Null(options.ServiceInstanceId);
        Assert.Null(options.DeploymentEnvironment);
    }

    [Fact]
    public void AdditionalResourceAttributes_defaults_to_empty_dictionary()
    {
        var options = new WitnessOptions();

        Assert.NotNull(options.AdditionalResourceAttributes);
        Assert.Empty(options.AdditionalResourceAttributes);
    }

    [Fact]
    public void AdditionalResourceAttributes_accepts_entries()
    {
        var options = new WitnessOptions();

        options.AdditionalResourceAttributes["host.region"] = "eu-west-1";

        Assert.Equal("eu-west-1", options.AdditionalResourceAttributes["host.region"]);
    }

    [Fact]
    public void CopyTo_copies_all_scalar_fields()
    {
        var source = new WitnessOptions
        {
            ServiceName = "svc",
            ServiceNamespace = "ns",
            ServiceVersion = "1.2.3",
            ServiceInstanceId = "i-1",
            DeploymentEnvironment = "prod",
        };
        var target = new WitnessOptions();

        source.CopyTo(target);

        Assert.Equal("svc", target.ServiceName);
        Assert.Equal("ns", target.ServiceNamespace);
        Assert.Equal("1.2.3", target.ServiceVersion);
        Assert.Equal("i-1", target.ServiceInstanceId);
        Assert.Equal("prod", target.DeploymentEnvironment);
    }

    [Fact]
    public void CopyTo_replaces_existing_target_resource_attributes()
    {
        var source = new WitnessOptions();
        source.AdditionalResourceAttributes["host.region"] = "eu-west-1";
        var target = new WitnessOptions();
        target.AdditionalResourceAttributes["stale.key"] = "stale.value";

        source.CopyTo(target);

        Assert.False(target.AdditionalResourceAttributes.ContainsKey("stale.key"));
        Assert.Equal("eu-west-1", target.AdditionalResourceAttributes["host.region"]);
    }
}
