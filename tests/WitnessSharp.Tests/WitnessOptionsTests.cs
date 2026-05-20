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
}
