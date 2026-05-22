using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;

namespace WitnessSharp.Tests;

public class WitnessMetricExtensionsTests : IDisposable
{
    private readonly Meter _meter = new("WitnessSharp.Tests.Metrics");
    private readonly ActivitySource _source = new("WitnessSharp.Tests.Metrics");

    public void Dispose()
    {
        _source.Dispose();
        _meter.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record Subject;

    private Witness<Subject> CreateWitness() =>
        new(_meter, _source, NullLogger<Subject>.Instance);

    [Fact]
    public void Counter_returns_counter_from_witness_meter()
    {
        var witness = CreateWitness();

        var counter = witness.Counter<int>("test.counter");

        Assert.NotNull(counter);
        Assert.Equal("test.counter", counter.Name);
    }

    [Fact]
    public void Counter_returns_same_instance_on_repeated_calls()
    {
        var witness = CreateWitness();

        var first = witness.Counter<int>("test.counter");
        var second = witness.Counter<int>("test.counter");

        Assert.Same(first, second);
    }

    [Fact]
    public void Counter_records_measurements()
    {
        var witness = CreateWitness();
        var measurements = new List<int>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter == _meter)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<int>((_, value, _, _) => measurements.Add(value));
        listener.Start();

        witness.Counter<int>("test.counter").Add(5);

        Assert.Contains(5, measurements);
    }

    [Fact]
    public void Histogram_returns_histogram_from_witness_meter()
    {
        var witness = CreateWitness();

        var histogram = witness.Histogram<double>("test.histogram");

        Assert.NotNull(histogram);
        Assert.Equal("test.histogram", histogram.Name);
    }

    [Fact]
    public void Histogram_returns_same_instance_on_repeated_calls()
    {
        var witness = CreateWitness();

        var first = witness.Histogram<double>("test.histogram");
        var second = witness.Histogram<double>("test.histogram");

        Assert.Same(first, second);
    }

    [Fact]
    public void Histogram_records_measurements()
    {
        var witness = CreateWitness();
        var measurements = new List<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter == _meter)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, value, _, _) => measurements.Add(value));
        listener.Start();

        witness.Histogram<double>("test.histogram").Record(3.14);

        Assert.Contains(3.14, measurements);
    }

    [Fact]
    public void UpDownCounter_returns_updowncounter_from_witness_meter()
    {
        var witness = CreateWitness();

        var counter = witness.UpDownCounter<int>("test.updown");

        Assert.NotNull(counter);
        Assert.Equal("test.updown", counter.Name);
    }

    [Fact]
    public void UpDownCounter_returns_same_instance_on_repeated_calls()
    {
        var witness = CreateWitness();

        var first = witness.UpDownCounter<int>("test.updown");
        var second = witness.UpDownCounter<int>("test.updown");

        Assert.Same(first, second);
    }

    [Fact]
    public void Gauge_returns_gauge_from_witness_meter()
    {
        var witness = CreateWitness();

        var gauge = witness.Gauge<double>("test.gauge");

        Assert.NotNull(gauge);
        Assert.Equal("test.gauge", gauge.Name);
    }

    [Fact]
    public void Gauge_returns_same_instance_on_repeated_calls()
    {
        var witness = CreateWitness();

        var first = witness.Gauge<double>("test.gauge");
        var second = witness.Gauge<double>("test.gauge");

        Assert.Same(first, second);
    }

    [Fact]
    public void Extensions_work_on_non_generic_IWitness()
    {
        IWitness witness = CreateWitness();

        var counter = witness.Counter<int>("test.base.counter");

        Assert.NotNull(counter);
        Assert.Equal("test.base.counter", counter.Name);
    }
}
