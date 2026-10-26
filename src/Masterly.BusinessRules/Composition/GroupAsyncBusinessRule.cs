using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// An immutable N-ary quantifier over async member rules, created through <see cref="AsyncRuleGroup"/>.
    /// The group counts how many members pass and compares the count with <see cref="Threshold"/> according to
    /// <see cref="Quantifier"/>. By default every member is evaluated sequentially so that
    /// <see cref="BusinessRuleResult.Details"/> lists every broken member; <see cref="WithShortCircuit"/> returns a copy
    /// that stops as soon as the verdict is decided, and <see cref="WithParallelEvaluation"/> returns a copy that starts
    /// every member at once. The two options are mutually exclusive.
    /// </summary>
    /// <remarks>
    /// In parallel mode every member task is awaited, even after the verdict is decided, so faults are always observed.
    /// A member that observes cancellation throws <see cref="OperationCanceledException"/> before it populates any cache
    /// (see <see cref="CachedAsyncBusinessRule"/>) or reaches a checker observer.
    /// </remarks>
    /// <example>
    /// <code>
    /// IAsyncBusinessRule any = AsyncRuleGroup.Any("PAYMENT.NONE_VALID", "At least one payment method must be valid", a1, a2)
    ///     .WithParallelEvaluation();
    /// </code>
    /// </example>
    public sealed class GroupAsyncBusinessRule : IComposedAsyncBusinessRule, IExplainableAsyncBusinessRule
    {
        private const string ParallelAndShortCircuitMessage = "Parallel evaluation and short-circuit cannot be combined on the same rule group.";
        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();
        private static readonly IReadOnlyList<BusinessRuleResult> NoDetails = Array.Empty<BusinessRuleResult>();

        private readonly IReadOnlyList<IAsyncBusinessRule> _members;

        internal GroupAsyncBusinessRule(
            RuleGroupQuantifier quantifier,
            int threshold,
            string code,
            string message,
            RuleSeverity severity,
            IReadOnlyList<IAsyncBusinessRule> members,
            bool shortCircuit,
            bool parallelEvaluation)
        {
            Quantifier = quantifier;
            Threshold = threshold;
            Code = code;
            Message = message;
            Severity = severity;
            _members = members;
            ShortCircuit = shortCircuit;
            ParallelEvaluation = parallelEvaluation;
        }

        /// <inheritdoc />
        public string Code { get; }

        /// <inheritdoc />
        public string Message { get; }

        /// <inheritdoc />
        public RuleSeverity Severity { get; }

        /// <inheritdoc />
        public string Name => GetType().Name;

        /// <inheritdoc />
        public string Description => RuleGroupQuantifierLogic.Describe(Quantifier, Threshold, _members.Count);

        /// <inheritdoc />
        public string Category => string.Empty;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => EmptyTags;

        /// <summary>
        /// Gets the quantifier applied to the members.
        /// </summary>
        public RuleGroupQuantifier Quantifier { get; }

        /// <summary>
        /// Gets the number of members that must pass: the member count for <see cref="RuleGroupQuantifier.All"/>,
        /// one for <see cref="RuleGroupQuantifier.Any"/>, and the requested count for <c>AtLeast</c>/<c>Exactly</c>.
        /// </summary>
        public int Threshold { get; }

        /// <summary>
        /// Gets whether sequential evaluation stops as soon as the verdict is decided.
        /// </summary>
        public bool ShortCircuit { get; }

        /// <summary>
        /// Gets whether every member is started at once and all member tasks are awaited.
        /// </summary>
        public bool ParallelEvaluation { get; }

        /// <inheritdoc />
        public RuleOperator Operator => RuleGroupQuantifierLogic.ToOperator(Quantifier);

        /// <inheritdoc />
        public IReadOnlyList<IAsyncBusinessRule> Operands => _members;

        /// <summary>
        /// Returns a copy of this group that stops evaluating members as soon as the verdict is decided.
        /// Members that were not reached are reported as <see cref="RuleOutcome.NotEvaluated"/> by <c>ExplainAsync</c>.
        /// </summary>
        /// <returns>A new short-circuiting group; this instance is unchanged.</returns>
        /// <exception cref="InvalidOperationException">Thrown when this group already evaluates in parallel.</exception>
        public GroupAsyncBusinessRule WithShortCircuit()
        {
            if (ParallelEvaluation)
            {
                throw new InvalidOperationException(ParallelAndShortCircuitMessage);
            }

            return new GroupAsyncBusinessRule(Quantifier, Threshold, Code, Message, Severity, _members, shortCircuit: true, parallelEvaluation: false);
        }

        /// <summary>
        /// Returns a copy of this group that starts every member at once and awaits all of them before deciding.
        /// </summary>
        /// <returns>A new parallel group; this instance is unchanged.</returns>
        /// <exception cref="InvalidOperationException">Thrown when this group already short-circuits.</exception>
        public GroupAsyncBusinessRule WithParallelEvaluation()
        {
            if (ShortCircuit)
            {
                throw new InvalidOperationException(ParallelAndShortCircuitMessage);
            }

            return new GroupAsyncBusinessRule(Quantifier, Threshold, Code, Message, Severity, _members, shortCircuit: false, parallelEvaluation: true);
        }

        /// <summary>
        /// Returns a copy of this group with a different severity.
        /// </summary>
        /// <param name="severity">The severity reported when the group is broken.</param>
        /// <returns>A new group; this instance is unchanged.</returns>
        public GroupAsyncBusinessRule WithSeverity(RuleSeverity severity)
        {
            return new GroupAsyncBusinessRule(Quantifier, Threshold, Code, Message, severity, _members, ShortCircuit, ParallelEvaluation);
        }

        /// <inheritdoc />
        public async Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => await EvaluateCoreAsync(context, null, cancellationToken) != null;

        /// <inheritdoc />
        /// <remarks>The returned result carries the broken members in <see cref="BusinessRuleResult.Details"/>.</remarks>
        public Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => EvaluateCoreAsync(context, null, cancellationToken);

        /// <inheritdoc />
        /// <exception cref="BusinessRuleValidationException">Thrown when the group is broken; the single result carries the broken members in <see cref="BusinessRuleResult.Details"/>.</exception>
        public async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            BusinessRuleResult? result = await EvaluateCoreAsync(context, null, cancellationToken);
            if (result != null)
            {
                throw new BusinessRuleValidationException(new[] { result });
            }
        }

        Task<BusinessRuleResult?> IExplainableAsyncBusinessRule.EvaluateCoreAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken)
            => EvaluateCoreAsync(context, trace, cancellationToken);

        private Task<BusinessRuleResult?> EvaluateCoreAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken)
        {
            return ParallelEvaluation
                ? EvaluateParallelAsync(context, trace, cancellationToken)
                : EvaluateSequentialAsync(context, trace, cancellationToken);
        }

        private async Task<BusinessRuleResult?> EvaluateSequentialAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken)
        {
            RuleExplanationTrace? children = trace?.Branch();
            long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;

            int passedCount = 0;
            int brokenCount = 0;
            int evaluatedCount = 0;
            List<BusinessRuleResult>? details = null;

            for (int index = 0; index < _members.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BusinessRuleResult? memberResult = await AsyncBusinessRuleWalker.EvaluateNodeAsync(_members[index], context, children, cancellationToken);
                evaluatedCount++;

                if (memberResult != null)
                {
                    brokenCount++;
                    details = details ?? new List<BusinessRuleResult>();
                    details.Add(memberResult);
                }
                else
                {
                    passedCount++;
                }

                if (ShortCircuit && RuleGroupQuantifierLogic.IsDecided(Quantifier, Threshold, passedCount, brokenCount, _members.Count))
                {
                    break;
                }
            }

            bool isBroken = RuleGroupQuantifierLogic.IsBroken(Quantifier, Threshold, passedCount);

            if (trace != null && children != null)
            {
                AsyncBusinessRuleWalker.AddNotEvaluated(children, _members, evaluatedCount, RuleExplanationText.ShortCircuitReason);
                string operatorName = RuleExplanationText.OperatorLabel(Operator, Threshold);
                trace.Add(AsyncBusinessRuleWalker.Evaluated(this, isBroken, operatorName, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            }

            return isBroken ? new BusinessRuleResult(Code, Message, Severity, details ?? NoDetails) : null;
        }

        private async Task<BusinessRuleResult?> EvaluateParallelAsync(BusinessRuleContext context, RuleExplanationTrace? trace, CancellationToken cancellationToken)
        {
            long start = trace != null ? RuleExplanationTrace.StartTiming() : 0L;
            cancellationToken.ThrowIfCancellationRequested();

            RuleExplanationTrace?[] memberTraces = new RuleExplanationTrace?[_members.Count];
            Task<BusinessRuleResult?>[] memberTasks = new Task<BusinessRuleResult?>[_members.Count];
            for (int index = 0; index < _members.Count; index++)
            {
                memberTraces[index] = trace?.Branch();
                memberTasks[index] = EvaluateMemberAsync(_members[index], context, memberTraces[index], cancellationToken);
            }

            BusinessRuleResult?[] memberResults = await Task.WhenAll(memberTasks);

            int passedCount = 0;
            List<BusinessRuleResult>? details = null;
            foreach (BusinessRuleResult? memberResult in memberResults)
            {
                if (memberResult != null)
                {
                    details = details ?? new List<BusinessRuleResult>();
                    details.Add(memberResult);
                }
                else
                {
                    passedCount++;
                }
            }

            bool isBroken = RuleGroupQuantifierLogic.IsBroken(Quantifier, Threshold, passedCount);

            if (trace != null)
            {
                RuleExplanationTrace children = trace.Branch();
                foreach (RuleExplanationTrace? memberTrace in memberTraces)
                {
                    if (memberTrace != null)
                    {
                        children.AddRange(memberTrace);
                    }
                }

                string operatorName = RuleExplanationText.OperatorLabel(Operator, Threshold);
                trace.Add(AsyncBusinessRuleWalker.Evaluated(this, isBroken, operatorName, RuleExplanationTrace.ElapsedSince(start), children.Nodes));
            }

            return isBroken ? new BusinessRuleResult(Code, Message, Severity, details ?? NoDetails) : null;
        }

        private static async Task<BusinessRuleResult?> EvaluateMemberAsync(
            IAsyncBusinessRule member,
            BusinessRuleContext context,
            RuleExplanationTrace? trace,
            CancellationToken cancellationToken)
        {
            return await AsyncBusinessRuleWalker.EvaluateNodeAsync(member, context, trace, cancellationToken);
        }
    }
}
