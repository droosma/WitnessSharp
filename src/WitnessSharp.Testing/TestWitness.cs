using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace WitnessSharp.Testing;

public sealed class TestWitness<T> : IWitness<T>, IDisposable
{
    private readonly TestLogger<T> _logger = new();
    private readonly List<RecordedMetric> _metrics = [];
    private readonly List<StartedActivity> _activities = [];
    private readonly MeterListener _meterListener;
    private readonly ActivityListener _activityListener;

    public Meter Meter { get; }
    public ActivitySource ActivitySource { get; }
    public ILogger<T> Logger => _logger;
    ILogger IWitness.Logger => _logger;

    public IReadOnlyList<LoggedMessage> LoggedMessages => _logger.Messages;
    public IReadOnlyList<RecordedMetric> RecordedMetrics => _metrics;
    public IReadOnlyList<StartedActivity> StartedActivities => _activities;

    public TestWitness()
    {
        var name = typeof(T).FullName ?? typeof(T).Name;
        Meter = new Meter($"TestWitness.{name}");
        ActivitySource = new ActivitySource($"TestWitness.{name}");

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter == Meter)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        _meterListener.SetMeasurementEventCallback<int>(OnMeasurement);
        _meterListener.SetMeasurementEventCallback<long>(OnMeasurement);
        _meterListener.SetMeasurementEventCallback<float>(OnMeasurement);
        _meterListener.SetMeasurementEventCallback<double>(OnMeasurement);
        _meterListener.SetMeasurementEventCallback<decimal>(OnMeasurement);
        _meterListener.Start();

        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, ActivitySource),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                var tags = activity.Tags
                    .Select(tag => new KeyValuePair<string, object?>(tag.Key, tag.Value))
                    .ToList();
                _activities.Add(new StartedActivity(activity.DisplayName, tags, activity.Status));
            },
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    private void OnMeasurement<TMeasurement>(Instrument instrument, TMeasurement measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        _metrics.Add(new RecordedMetric(instrument.Name, measurement, tags.ToArray()));
    }

    public void Dispose()
    {
        _meterListener.Dispose();
        _activityListener.Dispose();
        Meter.Dispose();
        ActivitySource.Dispose();
    }
}
