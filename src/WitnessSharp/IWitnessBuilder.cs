using Microsoft.Extensions.DependencyInjection;

namespace WitnessSharp;

public interface IWitnessBuilder
{
    IServiceCollection Services { get; }
}
