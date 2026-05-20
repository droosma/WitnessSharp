using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;

namespace WitnessSharp.Tests;

public class WitnessTests
{
    private sealed record Subject;

    [Fact]
    public void Exposes_injected_Meter()
    {
        using var meter = new Meter("test");
        using var source = new ActivitySource("test");
        var witness = new Witness<Subject>(meter, source, NullLogger<Subject>.Instance);

        Assert.Same(meter, witness.Meter);
    }

    [Fact]
    public void Exposes_injected_ActivitySource()
    {
        using var meter = new Meter("test");
        using var source = new ActivitySource("test");
        var witness = new Witness<Subject>(meter, source, NullLogger<Subject>.Instance);

        Assert.Same(source, witness.ActivitySource);
    }

    [Fact]
    public void Exposes_injected_typed_Logger()
    {
        using var meter = new Meter("test");
        using var source = new ActivitySource("test");
        var logger = NullLogger<Subject>.Instance;

        var witness = new Witness<Subject>(meter, source, logger);

        Assert.Same(logger, witness.Logger);
    }

    [Fact]
    public void Non_generic_Logger_returns_typed_Logger_instance()
    {
        using var meter = new Meter("test");
        using var source = new ActivitySource("test");
        var logger = NullLogger<Subject>.Instance;
        IWitness witness = new Witness<Subject>(meter, source, logger);

        Assert.Same(logger, witness.Logger);
    }

    [Fact]
    public void Implements_both_typed_and_non_generic_interfaces()
    {
        using var meter = new Meter("test");
        using var source = new ActivitySource("test");
        var witness = new Witness<Subject>(meter, source, NullLogger<Subject>.Instance);

        Assert.IsAssignableFrom<IWitness>(witness);
        Assert.IsAssignableFrom<IWitness<Subject>>(witness);
    }
}
