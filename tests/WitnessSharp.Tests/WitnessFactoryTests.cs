using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WitnessSharp.Tests;

public class WitnessFactoryTests
{
    private sealed record SubjectA;
    private sealed record SubjectB;

    [Fact]
    public void Create_returns_typed_IWitness()
    {
        using var meter = new Meter("test");
        using var source = new ActivitySource("test");
        var factory = new WitnessFactory(meter, source, NullLoggerFactory.Instance);

        var witness = factory.Create<SubjectA>();

        Assert.IsAssignableFrom<IWitness<SubjectA>>(witness);
    }

    [Fact]
    public void Created_witness_shares_factory_Meter()
    {
        using var meter = new Meter("test");
        using var source = new ActivitySource("test");
        var factory = new WitnessFactory(meter, source, NullLoggerFactory.Instance);

        var witness = factory.Create<SubjectA>();

        Assert.Same(meter, witness.Meter);
    }

    [Fact]
    public void Created_witness_shares_factory_ActivitySource()
    {
        using var meter = new Meter("test");
        using var source = new ActivitySource("test");
        var factory = new WitnessFactory(meter, source, NullLoggerFactory.Instance);

        var witness = factory.Create<SubjectA>();

        Assert.Same(source, witness.ActivitySource);
    }

    [Fact]
    public void Create_invokes_LoggerFactory_with_category_T()
    {
        using var meter = new Meter("test");
        using var source = new ActivitySource("test");
        var loggerFactory = new RecordingLoggerFactory();
        var factory = new WitnessFactory(meter, source, loggerFactory);

        _ = factory.Create<SubjectA>();
        _ = factory.Create<SubjectB>();

        // ILoggerFactory.CreateLogger<T>() reaches Logger<T> which substitutes
        // the nested-type delimiter '+' with '.' for friendlier log output.
        Assert.Equal(
            [
                typeof(SubjectA).FullName!.Replace('+', '.'),
                typeof(SubjectB).FullName!.Replace('+', '.'),
            ],
            loggerFactory.RequestedCategories);
    }

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public List<string> RequestedCategories { get; } = [];

        public ILogger CreateLogger(string categoryName)
        {
            RequestedCategories.Add(categoryName);
            return NullLogger.Instance;
        }

        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }
    }
}
