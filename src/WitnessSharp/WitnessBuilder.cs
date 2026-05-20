using Microsoft.Extensions.DependencyInjection;

namespace WitnessSharp;

public sealed class WitnessBuilder : IWitnessBuilder
{
    public IServiceCollection Services { get; }

    public WitnessBuilder(IServiceCollection services)
    {
        Services = services;
    }
}
