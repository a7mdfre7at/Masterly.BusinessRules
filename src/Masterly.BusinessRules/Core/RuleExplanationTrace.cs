using System;
using System.Collections.Generic;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Collects the <see cref="RuleExplanation"/> nodes produced while a rule tree is evaluated once.
    /// A trace is only created by the <c>Explain</c> extension methods; the plain <c>IsBroken</c>/<c>Check</c>
    /// paths pass <c>null</c> and therefore never allocate explanation nodes or timing state.
    /// </summary>
    internal sealed class RuleExplanationTrace
    {
        private readonly List<RuleExplanation> _nodes = new List<RuleExplanation>();

        /// <summary>
        /// Initializes a new trace.
        /// </summary>
        /// <param name="captureExceptions">When true, a node that throws is recorded as <see cref="RuleOutcome.Faulted"/> instead of propagating.</param>
        public RuleExplanationTrace(bool captureExceptions)
        {
            CaptureExceptions = captureExceptions;
        }

        /// <summary>
        /// Gets whether exceptions thrown by nodes are captured as <see cref="RuleOutcome.Faulted"/> nodes.
        /// </summary>
        public bool CaptureExceptions { get; }

        /// <summary>
        /// Gets the nodes recorded at this level, in evaluation order.
        /// </summary>
        public IReadOnlyList<RuleExplanation> Nodes => _nodes;

        /// <summary>
        /// Creates an empty child trace that shares this trace's options.
        /// </summary>
        public RuleExplanationTrace Branch()
        {
            return new RuleExplanationTrace(CaptureExceptions);
        }

        /// <summary>
        /// Records a node at this level.
        /// </summary>
        public void Add(RuleExplanation node)
        {
            _nodes.Add(node);
        }

        /// <summary>
        /// Appends every node of another trace to this level, preserving order.
        /// </summary>
        public void AddRange(RuleExplanationTrace other)
        {
            _nodes.AddRange(other._nodes);
        }

        /// <summary>
        /// Returns true when the exception should be captured as a faulted node rather than propagated.
        /// Cancellation is never captured.
        /// </summary>
        public bool ShouldCapture(Exception exception)
        {
            return CaptureExceptions && !(exception is OperationCanceledException);
        }

        /// <summary>
        /// Starts a high-resolution timing measurement. Delegates to <see cref="RuleTiming.Start"/>.
        /// </summary>
        public static long StartTiming()
        {
            return RuleTiming.Start();
        }

        /// <summary>
        /// Returns the time elapsed since <paramref name="start"/> was taken with <see cref="StartTiming"/>.
        /// Delegates to <see cref="RuleTiming.ElapsedSince"/>.
        /// </summary>
        public static TimeSpan ElapsedSince(long start)
        {
            return RuleTiming.ElapsedSince(start);
        }
    }
}
