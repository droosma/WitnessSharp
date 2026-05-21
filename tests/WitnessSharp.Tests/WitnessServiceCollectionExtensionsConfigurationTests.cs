using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WitnessSharp.Tests;

public class WitnessServiceCollectionExtensionsConfigurationTests
{
    private static IConfiguration BuildConfig(params (string Key, string? Value)[] entries)
    {
        var values = entries.ToDictionary(e => e.Key, e => e.Value);
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void AddWitness_with_IConfiguration_returns_a_WitnessBuilder()
    {
        var services = new ServiceCollection();
        var config = BuildConfig(("ServiceName", "FromConfig"));

        var builder = services.AddWitness(config);

        Assert.IsAssignableFrom<IWitnessBuilder>(builder);
    }

    [Fact]
    public void AddWitness_binds_WitnessOptions_from_configuration_section()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var config = BuildConfig(
            ("Witness:ServiceName", "FromConfig"),
            ("Witness:ServiceNamespace", "team-x"),
            ("Witness:ServiceVersion", "9.9.9"));

        services.AddWitness(config.GetSection("Witness"));
        using var sp = services.BuildServiceProvider();

        var opts = sp.GetRequiredService<IOptions<WitnessOptions>>().Value;
        Assert.Equal("FromConfig", opts.ServiceName);
        Assert.Equal("team-x", opts.ServiceNamespace);
        Assert.Equal("9.9.9", opts.ServiceVersion);
    }

    [Fact]
    public void AddWitness_binds_AdditionalResourceAttributes_from_configuration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var config = BuildConfig(
            ("Witness:ServiceName", "x"),
            ("Witness:AdditionalResourceAttributes:host.region", "eu-west-1"));

        services.AddWitness(config.GetSection("Witness"));
        using var sp = services.BuildServiceProvider();

        var opts = sp.GetRequiredService<IOptions<WitnessOptions>>().Value;
        Assert.Equal("eu-west-1", opts.AdditionalResourceAttributes["host.region"]);
    }
}
