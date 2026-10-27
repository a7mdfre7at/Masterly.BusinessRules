using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// The three instruments of the <c>Masterly.BusinessRules</c> <see cref="System.Diagnostics.Metrics.Meter"/>:
    /// a counter of rule evaluations, a histogram of evaluation durations and a counter of batches. Subscribe with plain
    /// OpenTelemetry (<c>metrics.AddMeter(BusinessRulesMetrics.MeterName)</c>). The <c>code</c> tag is written only
    /// when the caller selected one (see <see cref="TracingRuleObserverOptions.CodeTagSelector"/>) and never on the
    /// histogram, so cardinality stays bounded by category, severity and outcome.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddOpenTelemetry()
    ///     .WithTracing(t => t.AddSource(BusinessRulesActivitySource.Name))
    ///     .WithMetrics(m => m.AddMeter(BusinessRulesMetrics.MeterName));
    /// </code>
    /// </example>
    public sealed class BusinessRulesMetrics
    {
        /// <summary>
        /// The meter name: <c>Masterly.BusinessRules</c>.
        /// </summary>
        public const string MeterName = "Masterly.BusinessRules";

        /// <summary>
        /// The name of the evaluations counter: one increment per rule evaluation, tagged with <see cref="CodeTag"/>
        /// (when selected), <see cref="CategoryTag"/>, <see cref="SeverityTag"/> and <see cref="OutcomeTag"/>.
        /// </summary>
        public const string EvaluationsInstrumentName = "business_rules.evaluations";

        /// <summary>
        /// The name of the duration histogram: the wall-clock time of one rule evaluation in milliseconds, tagged with
        /// <see cref="OutcomeTag"/> only.
        /// </summary>
        public const string EvaluationDurationInstrumentName = "business_rules.evaluation.duration";

        /// <summary>
        /// The name of the batches counter: one increment per checker batch, tagged with <see cref="OutcomeTag"/>.
        /// </summary>
        public const string BatchesInstrumentName = "business_rules.batches";

        /// <summary>
        /// The unit of the duration histogram.
        /// </summary>
        public const string DurationUnit = "ms";

        /// <summary>
        /// The tag carrying the rule code.
        /// </summary>
        public const string CodeTag = "code";

        /// <summary>
        /// The tag carrying the rule category.
        /// </summary>
        public const string CategoryTag = "category";

        /// <summary>
        /// The tag carrying the rule severity (see <see cref="RuleSeverityTags"/>).
        /// </summary>
        public const string SeverityTag = "severity";

        /// <summary>
        /// The tag carrying the outcome (see <see cref="RuleOutcomeTags"/>).
        /// </summary>
        public const string OutcomeTag = "outcome";

        private const string EvaluationsDescription = "Number of business rule evaluations.";
        private const string EvaluationDurationDescription = "Wall-clock duration of one business rule evaluation.";
        private const string BatchesDescription = "Number of business rule batches evaluated by the checkers.";

        private readonly Counter<long> _evaluations;
        private readonly Histogram<double> _evaluationDuration;
        private readonly Counter<long> _batches;

        /// <summary>
        /// Initializes the instruments on <paramref name="meter"/>. Use this constructor to record on a meter you own
        /// (for example one created by an <c>IMeterFactory</c> or a test); production code normally uses <see cref="Default"/>.
        /// </summary>
        /// <param name="meter">The meter to create the instruments on.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="meter"/> is null.</exception>
        public BusinessRulesMetrics(Meter meter)
        {
            Meter = meter ?? throw new ArgumentNullException(nameof(meter));
            _evaluations = meter.CreateCounter<long>(EvaluationsInstrumentName, null, EvaluationsDescription);
            _evaluationDuration = meter.CreateHistogram<double>(EvaluationDurationInstrumentName, DurationUnit, EvaluationDurationDescription);
            _batches = meter.CreateCounter<long>(BatchesInstrumentName, null, BatchesDescription);
        }

        /// <summary>
        /// Gets the shared instance over the meter named <see cref="MeterName"/>, used by every observer and decorator unless
        /// <see cref="TracingRuleObserverOptions.Metrics"/> says otherwise.
        /// </summary>
        public static BusinessRulesMetrics Default { get; } = new BusinessRulesMetrics(new Meter(MeterName, ReadVersion()));

        /// <summary>
        /// Gets the meter the instruments belong to.
        /// </summary>
        public Meter Meter { get; }

        /// <summary>
        /// Records one rule evaluation: increments the evaluations counter and, when a duration is known, records it on the
        /// histogram.
        /// </summary>
        /// <param name="rule">The evaluated rule; its category and severity become tags.</param>
        /// <param name="code">The code to tag the counter with, or null to leave the evaluation untagged by code.</param>
        /// <param name="outcome">The outcome tag value, one of <see cref="RuleOutcomeTags"/>.</param>
        /// <param name="duration">The evaluation duration, or null when it is not known (a faulted rule whose span was not sampled).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> or <paramref name="outcome"/> is null.</exception>
        public void RecordEvaluation(IBusinessRuleMetadata rule, string? code, string outcome, TimeSpan? duration)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            RecordEvaluation(RuleMetadataSnapshot.Capture(rule), code, outcome, duration);
        }

        /// <summary>
        /// Records one batch: increments the batches counter with the outcome tag.
        /// </summary>
        /// <param name="outcome">The outcome tag value, one of <see cref="RuleOutcomeTags"/> (see <see cref="RuleOutcomeTags.ForBatch"/>).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="outcome"/> is null.</exception>
        public void RecordBatch(string outcome)
        {
            if (outcome == null)
            {
                throw new ArgumentNullException(nameof(outcome));
            }

            _batches.Add(1, new TagList { { OutcomeTag, outcome } });
        }

        /// <summary>
        /// Records one rule evaluation from an already captured metadata snapshot.
        /// </summary>
        internal void RecordEvaluation(RuleMetadataSnapshot metadata, string? code, string outcome, TimeSpan? duration)
        {
            if (outcome == null)
            {
                throw new ArgumentNullException(nameof(outcome));
            }

            TagList tags = new TagList
            {
                { CategoryTag, metadata.Category },
                { SeverityTag, RuleSeverityTags.From(metadata.Severity) },
                { OutcomeTag, outcome }
            };

            if (code != null)
            {
                tags.Add(CodeTag, code);
            }

            _evaluations.Add(1, tags);

            if (duration.HasValue)
            {
                _evaluationDuration.Record(duration.Value.TotalMilliseconds, new TagList { { OutcomeTag, outcome } });
            }
        }

        private static string ReadVersion()
        {
            return typeof(BusinessRulesMetrics).GetTypeInfo().Assembly.GetName().Version?.ToString() ?? string.Empty;
        }
    }
}
