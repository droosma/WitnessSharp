using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace WitnessSharp.Testing;

/// <summary>
/// An in-memory <see cref="IWitness{T}"/> test double that records logged messages, recorded metric
/// measurements and stopped activities for assertion. Thread-safe: the listener callbacks that capture
/// metrics and activities may run on arbitrary threads.
/// </summary>
/// <typeparam name="T">The type used as the logger category.</typeparam>
public sealed class TestWitness<T> : IWitness<T>, IDisposable
{
    private readonly object _gate = new();
    private readonly TestLogger<T> _logger = new();
    private readonly List<RecordedMetric> _metrics = [];
    private readonly List<StartedActivity> _activities = [];
    private readonly MeterListener _meterListener;
    private readonly ActivityListener _activityListener;

    /// <inheritdoc/>
    public Meter Meter { get; }

    /// <inheritdoc/>
    public ActivitySource ActivitySource { get; }

    /// <inheritdoc/>
    public ILogger<T> Logger => _logger;

    ILogger IWitness.Logger => _logger;

    /// <summary>Gets a snapshot of the messages logged so far.</summary>
    public IReadOnlyList<LoggedMessage> LoggedMessages => _logger.Messages;

    /// <summary>Gets a snapshot of the metric measurements recorded so far.</summary>
    public IReadOnlyList<RecordedMetric> RecordedMetrics
    {
        get
        {
            lock (_gate)
            {
                return _metrics.ToArray();
            }
        }
    }

    /// <summary>Gets a snapshot of the activities started (and stopped) so far.</summary>
    public IReadOnlyList<StartedActivity> StartedActivities
    {
        get
        {
            lock (_gate)
            {
                return _activities.ToArray();
            }
        }
    }

    /// <summary>Initializes a new instance of the <see cref="TestWitness{T}"/> class.</summary>
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
                lock (_gate)
                {
                    _activities.Add(new StartedActivity(activity.DisplayName, tags, activity.Status));
                }
            },
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    private void OnMeasurement<TMeasurement>(Instrument instrument, TMeasurement measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        var recorded = new RecordedMetric(instrument.Name, measurement, tags.ToArray());
        lock (_gate)
        {
            _metrics.Add(recorded);
        }
    }

    /// <summary>Disposes the listeners, meter and activity source.</summary>
    public void Dispose()
    {
        _meterListener.Dispose();
        _activityListener.Dispose();
        Meter.Dispose();
        ActivitySource.Dispose();
    }
}
