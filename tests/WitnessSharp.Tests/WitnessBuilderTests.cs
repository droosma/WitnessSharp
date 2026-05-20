using Microsoft.Extensions.DependencyInjection;

namespace WitnessSharp.Tests;

public class WitnessBuilderTests
{
    [Fact]
    public void Services_property_returns_constructor_argument()
    {
        var services = new ServiceCollection();
        var builder = new WitnessBuilder(services);

        Assert.Same(services, builder.Services);
    }

    [Fact]
    public void Implements_IWitnessBuilder()
    {
        var services = new ServiceCollection();
        var builder = new WitnessBuilder(services);

        Assert.IsAssignableFrom<IWitnessBuilder>(builder);
    }
}
