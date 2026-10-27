using System;

namespace Masterly.BusinessRules.Diagnostics
{
    /// <summary>
    /// Options shared by <see cref="TracingRuleExecutionObserver"/>, <see cref="TracingAsyncRuleExecutionObserver"/>
    /// and the <c>Traced()</c> decorators. The defaults are conservative: no message is copied onto a span and only
    /// leaf rule codes become a tag, so a composed rule's synthesised code (<c>A+B</c>) cannot explode cardinality.
    /// </summary>
    /// <example>
    /// <code>
    /// TracingRuleObserverOptions options = new TracingRuleObserverOptions
    /// {
    ///     MessageAsEvent = false,
    ///     CodeTagSelector = rule => rule is IComposedBusinessRule ? null : rule.Code
    /// };
    /// </code>
    /// </example>
    public sealed class TracingRuleObserverOptions
    {
        private Func<IBusinessRuleMetadata, string?> _codeTagSelector = LeafCodeTagSelector;
        private BusinessRulesMetrics _metrics = BusinessRulesMetrics.Default;

        /// <summary>
        /// Gets or sets whether a broken rule's message is added to its span as a <see cref="BusinessRuleSpanAttributes.BrokenEventName"/>
        /// event (and a shadow violation's message to a <see cref="BusinessRuleSpanAttributes.ShadowViolationEventName"/> event).
        /// Defaults to false because a rule message can carry domain data.
        /// </summary>
        public bool MessageAsEvent { get; set; }

        /// <summary>
        /// Gets or sets the delegate choosing the value of the <see cref="BusinessRuleSpanAttributes.Code"/> span attribute,
        /// the span display name and the <c>code</c> tag of the evaluations counter. Return null to leave the rule untagged.
        /// Defaults to <see cref="LeafCodeTagSelector"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public Func<IBusinessRuleMetadata, string?> CodeTagSelector
        {
            get => _codeTagSelector;
            set => _codeTagSelector = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Gets or sets whether the <see cref="BusinessRulesMetrics"/> instruments are recorded alongside the spans. Defaults to true.
        /// </summary>
        public bool RecordMetrics { get; set; } = true;

        /// <summary>
        /// Gets or sets the metrics instance measurements are recorded on. Defaults to <see cref="BusinessRulesMetrics.Default"/>,
        /// whose <see cref="System.Diagnostics.Metrics.Meter"/> is named <see cref="BusinessRulesMetrics.MeterName"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public BusinessRulesMetrics Metrics
        {
            get => _metrics;
            set => _metrics = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// The default <see cref="CodeTagSelector"/>: unwraps decorators (<see cref="IDecoratedBusinessRule"/>,
        /// <see cref="IDecoratedAsyncBusinessRule"/>, <see cref="SyncToAsyncRuleAdapter"/>, <see cref="AsyncToSyncRuleAdapter"/>)
        /// and returns null when the innermost rule is composed (<see cref="IComposedBusinessRule"/> /
        /// <see cref="IComposedAsyncBusinessRule"/>: its code is synthesised from its operands), otherwise the rule's <c>Code</c>.
        /// </summary>
        /// <param name="rule">The rule about to be recorded.</param>
        /// <returns>The code to tag, or null for a composed rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rule"/> is null.</exception>
        public static string? LeafCodeTagSelector(IBusinessRuleMetadata rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            IBusinessRuleMetadata innermost = Unwrap(rule);
            if (innermost is IComposedBusinessRule || innermost is IComposedAsyncBusinessRule)
            {
                return null;
            }

            return rule.Code;
        }

        /// <summary>
        /// Returns the selected code tag for <paramref name="rule"/>, treating a selector that throws as "untagged".
        /// </summary>
        internal string? SelectCodeTag(IBusinessRuleMetadata rule)
        {
            try
            {
                return _codeTagSelector(rule);
            }
            catch (Exception)
            {
                // The selector is user code running inside an observer callback; a tag is never worth failing the batch.
                return null;
            }
        }

        private static IBusinessRuleMetadata Unwrap(IBusinessRuleMetadata rule)
        {
            IBusinessRuleMetadata current = rule;
            while (true)
            {
                switch (current)
                {
                    case IDecoratedBusinessRule decorated:
                        current = decorated.InnerRule;
                        break;
                    case IDecoratedAsyncBusinessRule decoratedAsync:
                        current = decoratedAsync.InnerRule;
                        break;
                    case SyncToAsyncRuleAdapter syncToAsync:
                        current = syncToAsync.InnerRule;
                        break;
                    case AsyncToSyncRuleAdapter asyncToSync:
                        current = asyncToSync.InnerRule;
                        break;
                    default:
                        return current;
                }
            }
        }
    }
}
