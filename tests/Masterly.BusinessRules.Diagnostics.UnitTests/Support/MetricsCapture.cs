using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Masterly.BusinessRules.Diagnostics.UnitTests.Support;

/// <summary>
/// One recorded measurement.
/// </summary>
public sealed class Measurement
{
    public Measurement(string instrument, double value, IReadOnlyDictionary<string, object?> tags)
    {
        Instrument = instrument;
        Value = value;
        Tags = tags;
    }

    public string Instrument { get; }

    public double Value { get; }

    public IReadOnlyDictionary<string, object?> Tags { get; }

    public object? this[string tag] => Tags.TryGetValue(tag, out object? value) ? value : null;
}

/// <summary>
/// Owns a private <see cref="Meter"/> plus a <see cref="BusinessRulesMetrics"/> over it and records every measurement,
/// so concurrently running tests never see each other's numbers.
/// </summary>
public sealed class MetricsCapture : IDisposable
{
    private readonly MeterListener _listener;
    private readonly ConcurrentQueue<Measurement> _measurements = new ConcurrentQueue<Measurement>();

    public MetricsCapture()
    {
        Meter = new Meter($"{BusinessRulesMetrics.MeterName}.Tests.{Guid.NewGuid():N}");
        Metrics = new BusinessRulesMetrics(Meter);
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument.Meter, Meter))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => Record(instrument, value, tags));
        _listener.Start();
    }

    public Meter Meter { get; }

    public BusinessRulesMetrics Metrics { get; }

    public IReadOnlyList<Measurement> Measurements => _measurements.ToArray();

    public IReadOnlyList<Measurement> Evaluations => Of(BusinessRulesMetrics.EvaluationsInstrumentName);

    public IReadOnlyList<Measurement> Durations => Of(BusinessRulesMetrics.EvaluationDurationInstrumentName);

    public IReadOnlyList<Measurement> Batches => Of(BusinessRulesMetrics.BatchesInstrumentName);

    public TracingRuleObserverOptions TracingOptions(Action<TracingRuleObserverOptions>? configure = null)
    {
        TracingRuleObserverOptions options = new TracingRuleObserverOptions { Metrics = Metrics };
        configure?.Invoke(options);
        return options;
    }

    public void Dispose()
    {
        _listener.Dispose();
        Meter.Dispose();
    }

    private IReadOnlyList<Measurement> Of(string instrument)
    {
        return Measurements.Where(measurement => measurement.Instrument == instrument).ToList();
    }

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        Dictionary<string, object?> copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> tag in tags)
        {
            copy[tag.Key] = tag.Value;
        }

        _measurements.Enqueue(new Measurement(instrument.Name, value, copy));
    }
}
