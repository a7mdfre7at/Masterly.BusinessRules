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
        private readonly RuleExplanationTrace? _parent;

        /// <summary>
        /// Initializes a new trace.
        /// </summary>
        /// <param name="captureExceptions">When true, a node that throws is recorded as <see cref="RuleOutcome.Faulted"/> instead of propagating.</param>
        public RuleExplanationTrace(bool captureExceptions)
            : this(captureExceptions, null)
        {
        }

        private RuleExplanationTrace(bool captureExceptions, RuleExplanationTrace? parent)
        {
            CaptureExceptions = captureExceptions;
            _parent = parent;
        }

        /// <summary>
        /// Gets whether a fault was captured at this level or in any branch created from it, so the verdict recorded
        /// here rests on a captured exception rather than on a completed evaluation (a cache must not keep it).
        /// </summary>
        public bool HasCapturedFault { get; private set; }

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
            return new RuleExplanationTrace(CaptureExceptions, this);
        }

        /// <summary>
        /// Records that a fault was captured at this level; the flag propagates to every ancestor trace.
        /// </summary>
        public void MarkFaultCaptured()
        {
            for (RuleExplanationTrace? level = this; level != null && !level.HasCapturedFault; level = level._parent)
            {
                level.HasCapturedFault = true;
            }
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
