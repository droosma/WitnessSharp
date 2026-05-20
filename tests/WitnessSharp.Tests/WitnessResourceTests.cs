using OpenTelemetry.Resources;

namespace WitnessSharp.Tests;

[Collection("EnvVarMutating")]
public class WitnessResourceTests
{
    private sealed class EnvVarOverride : IDisposable
    {
        private readonly string _key;
        private readonly string? _original;

        public EnvVarOverride(string key, string? value)
        {
            _key = key;
            _original = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_key, _original);
    }

    private static IReadOnlyDictionary<string, object> BuildAttributes(WitnessOptions options)
    {
        var builder = ResourceBuilder.CreateEmpty();
        WitnessResource.Apply(builder, options);
        return builder.Build().Attributes.ToDictionary(a => a.Key, a => a.Value);
    }

    // ----- service.* -----

    [Fact]
    public void Includes_service_name_when_ServiceName_is_set()
    {
        var attrs = BuildAttributes(new WitnessOptions { ServiceName = "MyService" });

        Assert.Equal("MyService", attrs["service.name"]);
    }

    [Fact]
    public void Omits_service_name_when_ServiceName_is_empty()
    {
        var attrs = BuildAttributes(new WitnessOptions { ServiceName = string.Empty });

        Assert.DoesNotContain("service.name", attrs.Keys);
    }

    [Fact]
    public void Includes_service_namespace_when_set()
    {
        var attrs = BuildAttributes(new WitnessOptions { ServiceName = "x", ServiceNamespace = "team-a" });

        Assert.Equal("team-a", attrs["service.namespace"]);
    }

    [Fact]
    public void Includes_service_version_when_set()
    {
        var attrs = BuildAttributes(new WitnessOptions { ServiceName = "x", ServiceVersion = "1.2.3" });

        Assert.Equal("1.2.3", attrs["service.version"]);
    }

    [Fact]
    public void Service_instance_id_uses_option_value_when_set()
    {
        var attrs = BuildAttributes(new WitnessOptions { ServiceName = "x", ServiceInstanceId = "host-42" });

        Assert.Equal("host-42", attrs["service.instance.id"]);
    }

    [Fact]
    public void Service_instance_id_defaults_to_machine_name()
    {
        var attrs = BuildAttributes(new WitnessOptions { ServiceName = "x" });

        Assert.Equal(Environment.MachineName, attrs["service.instance.id"]);
    }

    // ----- deployment.environment -----

    [Fact]
    public void Deployment_environment_uses_option_value_when_set()
    {
        var attrs = BuildAttributes(new WitnessOptions { ServiceName = "x", DeploymentEnvironment = "production" });

        Assert.Equal("production", attrs["deployment.environment"]);
    }

    [Fact]
    public void Deployment_environment_falls_back_to_DOTNET_ENVIRONMENT()
    {
        using (new EnvVarOverride("DOTNET_ENVIRONMENT", "Staging"))
        using (new EnvVarOverride("ASPNETCORE_ENVIRONMENT", null))
        {
            var attrs = BuildAttributes(new WitnessOptions { ServiceName = "x" });

            Assert.Equal("Staging", attrs["deployment.environment"]);
        }
    }

    [Fact]
    public void Deployment_environment_falls_back_to_ASPNETCORE_ENVIRONMENT()
    {
        using (new EnvVarOverride("DOTNET_ENVIRONMENT", null))
        using (new EnvVarOverride("ASPNETCORE_ENVIRONMENT", "Development"))
        {
            var attrs = BuildAttributes(new WitnessOptions { ServiceName = "x" });

            Assert.Equal("Development", attrs["deployment.environment"]);
        }
    }

    [Fact]
    public void Option_DeploymentEnvironment_wins_over_env_vars()
    {
        using (new EnvVarOverride("DOTNET_ENVIRONMENT", "Staging"))
        using (new EnvVarOverride("ASPNETCORE_ENVIRONMENT", "Development"))
        {
            var attrs = BuildAttributes(new WitnessOptions { ServiceName = "x", DeploymentEnvironment = "production" });

            Assert.Equal("production", attrs["deployment.environment"]);
        }
    }

    [Fact]
    public void DOTNET_ENVIRONMENT_wins_over_ASPNETCORE_ENVIRONMENT()
    {
        using (new EnvVarOverride("DOTNET_ENVIRONMENT", "Staging"))
        using (new EnvVarOverride("ASPNETCORE_ENVIRONMENT", "Development"))
        {
            var attrs = BuildAttributes(new WitnessOptions { ServiceName = "x" });

            Assert.Equal("Staging", attrs["deployment.environment"]);
        }
    }

    [Fact]
    public void Deployment_environment_omitted_when_no_source_provides_it()
    {
        using (new EnvVarOverride("DOTNET_ENVIRONMENT", null))
        using (new EnvVarOverride("ASPNETCORE_ENVIRONMENT", null))
        {
            var attrs = BuildAttributes(new WitnessOptions { ServiceName = "x" });

            Assert.DoesNotContain("deployment.environment", attrs.Keys);
        }
    }

    // ----- additional attributes -----

    [Fact]
    public void AdditionalResourceAttributes_are_copied_through()
    {
        var options = new WitnessOptions { ServiceName = "x" };
        options.AdditionalResourceAttributes["host.region"] = "eu-west-1";
        options.AdditionalResourceAttributes["team.owner"] = "platform";

        var attrs = BuildAttributes(options);

        Assert.Equal("eu-west-1", attrs["host.region"]);
        Assert.Equal("platform", attrs["team.owner"]);
    }
}

[CollectionDefinition("EnvVarMutating", DisableParallelization = true)]
public class EnvVarMutatingCollection;
