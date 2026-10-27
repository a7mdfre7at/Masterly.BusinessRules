using System.Collections.Concurrent;
using System.Diagnostics;

namespace Masterly.BusinessRules.Diagnostics.UnitTests.Support;

/// <summary>
/// Subscribes to <see cref="BusinessRulesActivitySource"/> for the lifetime of a test and records the spans it produces.
/// Every test using it belongs to <see cref="DiagnosticsCollections.Activities"/>, so the recorded spans are its own.
/// </summary>
public sealed class ActivityCapture : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly ConcurrentQueue<Activity> _started = new ConcurrentQueue<Activity>();
    private readonly ConcurrentQueue<Activity> _stopped = new ConcurrentQueue<Activity>();

    public ActivityCapture()
    {
        Activity.Current = null;
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BusinessRulesActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _started.Enqueue(activity),
            ActivityStopped = activity => _stopped.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public IReadOnlyList<Activity> Started => _started.ToArray();

    public IReadOnlyList<Activity> Stopped => _stopped.ToArray();

    public IReadOnlyList<Activity> RuleSpans => Stopped.Where(activity => activity.OperationName == BusinessRulesActivitySource.RuleActivityName).ToList();

    public IReadOnlyList<Activity> BatchSpans => Stopped.Where(activity => activity.OperationName == BusinessRulesActivitySource.BatchActivityName).ToList();

    public Activity SingleRuleSpan()
    {
        return Assert.Single(RuleSpans);
    }

    public Activity SingleBatchSpan()
    {
        return Assert.Single(BatchSpans);
    }

    public static string? Tag(Activity activity, string key)
    {
        return activity.GetTagItem(key) as string;
    }

    public void Dispose()
    {
        _listener.Dispose();
        Activity.Current = null;
    }
}
