using System;
using System.Diagnostics;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// Starts, finds and finishes the spans of <see cref="BusinessRulesActivitySource"/>. The observers keep no per-rule
    /// state: a rule span is made <see cref="Activity.Current"/> when the rule starts and is found again through the
    /// <see cref="Activity.Current"/> chain when the rule completes, which is what makes the same rule instance
    /// appearing twice in a batch, and parallel async batches, work without a lookup table.
    /// </summary>
    internal static class RuleActivities
    {
        /// <summary>
        /// Starts the batch span and makes it current. Returns null when nobody listens.
        /// </summary>
        public static Activity? StartBatch(int ruleCount)
        {
            Activity? activity = BusinessRulesActivitySource.Source.StartActivity(BusinessRulesActivitySource.BatchActivityName, ActivityKind.Internal);
            if (activity != null && activity.IsAllDataRequested)
            {
                activity.SetTag(BusinessRuleSpanAttributes.BatchRuleCount, ruleCount);
            }

            return activity;
        }

        /// <summary>
        /// Starts the span of one rule evaluation and makes it current, so <see cref="Activity.Current"/> is the rule
        /// span while the rule body runs. Returns null when nobody listens.
        /// </summary>
        public static Activity? StartRule(IBusinessRuleMetadata rule, RuleMetadataSnapshot metadata, string? codeTag)
        {
            Activity? activity = BusinessRulesActivitySource.Source.StartActivity(BusinessRulesActivitySource.RuleActivityName, ActivityKind.Internal);
            if (activity == null || !activity.IsAllDataRequested)
            {
                return activity;
            }

            if (codeTag != null)
            {
                activity.DisplayName = codeTag;
                activity.SetTag(BusinessRuleSpanAttributes.Code, codeTag);
            }

            activity.SetTag(BusinessRuleSpanAttributes.Name, metadata.Name);
            activity.SetTag(BusinessRuleSpanAttributes.Category, metadata.Category);
            activity.SetTag(BusinessRuleSpanAttributes.Severity, RuleSeverityTags.From(metadata.Severity));
            return activity;
        }

        /// <summary>
        /// Finds the innermost unfinished rule span in the current chain, without crossing a batch span. Foreign
        /// activities (for example one a rule started and never stopped) and finished spans are walked past.
        /// </summary>
        public static Activity? CurrentRule()
        {
            Activity? current = Activity.Current;
            while (current != null)
            {
                if (!IsOpenAndOurs(current))
                {
                    current = current.Parent;
                    continue;
                }

                return current.OperationName == BusinessRulesActivitySource.RuleActivityName ? current : null;
            }

            return null;
        }

        /// <summary>
        /// Finds the innermost unfinished batch span in the current chain.
        /// </summary>
        public static Activity? CurrentBatch()
        {
            Activity? current = Activity.Current;
            while (current != null)
            {
                if (IsOpenAndOurs(current) && current.OperationName == BusinessRulesActivitySource.BatchActivityName)
                {
                    return current;
                }

                current = current.Parent;
            }

            return null;
        }

        /// <summary>
        /// Finds the innermost unfinished span of this package (rule or batch) in the current chain, walking past foreign
        /// activities and finished spans.
        /// </summary>
        public static Activity? CurrentOwned()
        {
            Activity? current = Activity.Current;
            while (current != null && !IsOpenAndOurs(current))
            {
                current = current.Parent;
            }

            return current;
        }

        /// <summary>
        /// Adds the broken event (with the message) to a rule span.
        /// </summary>
        public static void AddBrokenEvent(Activity? activity, string message)
        {
            AddMessageEvent(activity, BusinessRuleSpanAttributes.BrokenEventName, null, message);
        }

        /// <summary>
        /// Adds the shadow-violation event to the enclosing span, naming the gated rule and optionally the message.
        /// </summary>
        public static void AddShadowEvent(Activity? activity, string? codeTag, string? message)
        {
            AddMessageEvent(activity, BusinessRuleSpanAttributes.ShadowViolationEventName, codeTag, message);
        }

        /// <summary>
        /// Writes the outcome and stops the rule span.
        /// </summary>
        public static void Complete(Activity? activity, string outcome)
        {
            if (activity == null)
            {
                return;
            }

            if (activity.IsAllDataRequested)
            {
                activity.SetTag(BusinessRuleSpanAttributes.Outcome, outcome);
            }

            activity.Stop();
        }

        /// <summary>
        /// Marks the rule span as faulted (error status plus the OpenTelemetry exception event) and stops it.
        /// </summary>
        public static void Fault(Activity? activity, Exception exception)
        {
            if (activity == null)
            {
                return;
            }

            if (activity.IsAllDataRequested)
            {
                activity.SetTag(BusinessRuleSpanAttributes.Outcome, RuleOutcomeTags.Faulted);
                activity.SetStatus(ActivityStatusCode.Error, exception.Message);
                AddExceptionEvent(activity, exception);
            }

            activity.Stop();
        }

        /// <summary>
        /// Writes the batch counts and outcome and stops the batch span. A batch with faulted rules carries an error status.
        /// </summary>
        public static void CompleteBatch(Activity? activity, BatchSummary summary)
        {
            if (activity == null)
            {
                return;
            }

            if (activity.IsAllDataRequested)
            {
                activity.SetTag(BusinessRuleSpanAttributes.BatchBrokenCount, summary.BrokenCount);
                activity.SetTag(BusinessRuleSpanAttributes.BatchFaultedCount, summary.FaultedCount);
                activity.SetTag(BusinessRuleSpanAttributes.BatchOutcome, summary.Outcome);

                if (summary.FaultedCount > 0)
                {
                    activity.SetStatus(ActivityStatusCode.Error, FaultedBatchDescription(summary.FaultedCount));
                }
            }

            activity.Stop();
        }

        /// <summary>
        /// Closes the batch span of a batch that ended by an exception and returns the outcome written:
        /// <see cref="RuleOutcomeTags.Cancelled"/> for a cancellation, otherwise <see cref="RuleOutcomeTags.Faulted"/>
        /// with an error status and the exception event.
        /// </summary>
        public static string AbortBatch(Activity? activity, Exception exception)
        {
            bool cancelled = exception is OperationCanceledException;
            string outcome = cancelled ? RuleOutcomeTags.Cancelled : RuleOutcomeTags.Faulted;
            if (activity == null)
            {
                return outcome;
            }

            if (activity.IsAllDataRequested)
            {
                activity.SetTag(BusinessRuleSpanAttributes.BatchOutcome, outcome);
                if (!cancelled)
                {
                    activity.SetStatus(ActivityStatusCode.Error, exception.Message);
                    AddExceptionEvent(activity, exception);
                }
            }

            activity.Stop();
            return outcome;
        }

        private static void AddExceptionEvent(Activity activity, Exception exception)
        {
            ActivityTagsCollection tags = new ActivityTagsCollection
            {
                { BusinessRuleSpanAttributes.ExceptionType, exception.GetType().FullName },
                { BusinessRuleSpanAttributes.ExceptionMessage, exception.Message }
            };
            activity.AddEvent(new ActivityEvent(BusinessRuleSpanAttributes.ExceptionEventName, DateTimeOffset.UtcNow, tags));
        }

        private static string FaultedBatchDescription(int faultedCount)
        {
            return faultedCount == 1 ? "1 rule faulted" : $"{faultedCount} rules faulted";
        }

        private static void AddMessageEvent(Activity? activity, string eventName, string? codeTag, string? message)
        {
            if (activity == null || !activity.IsAllDataRequested)
            {
                return;
            }

            ActivityTagsCollection tags = new ActivityTagsCollection();
            if (codeTag != null)
            {
                tags.Add(BusinessRuleSpanAttributes.Code, codeTag);
            }

            if (message != null)
            {
                tags.Add(BusinessRuleSpanAttributes.Message, message);
            }

            activity.AddEvent(new ActivityEvent(eventName, DateTimeOffset.UtcNow, tags));
        }

        private static bool IsOpenAndOurs(Activity activity)
        {
            return ReferenceEquals(activity.Source, BusinessRulesActivitySource.Source) && !activity.IsStopped;
        }
    }
}
