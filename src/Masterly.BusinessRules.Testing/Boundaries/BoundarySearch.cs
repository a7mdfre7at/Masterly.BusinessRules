using System;
using System.Collections.Generic;
using System.Numerics;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// The probe schedule of a boundary search as a state machine, so the sync and async finders share one algorithm:
    /// both endpoints first (which fixes the direction or proves there is no boundary), then bisection down to two
    /// adjacent grid points, then evenly spread samples that must agree with the located boundary or the rule is
    /// reported as non-monotone. The caller asks for the next index with <see cref="TryNextProbe"/>, evaluates the
    /// rule there and hands the verdict back with <see cref="Record"/>.
    /// </summary>
    /// <typeparam name="T">The numeric input type.</typeparam>
    internal sealed class BoundarySearch<T> where T : struct, INumber<T>
    {
        private readonly BoundaryGrid<T> _grid;
        private readonly int _sampleCount;
        private readonly HashSet<long> _probed = new HashSet<long>();
        private Phase _phase = Phase.LowEndpoint;
        private Queue<long>? _samples;
        private long? _pending;
        private long _low;
        private long _high;
        private bool _lowIsBroken;
        private int _probeCount;

        public BoundarySearch(BoundaryGrid<T> grid, int sampleCount)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));

            if (sampleCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount), sampleCount, RuleBoundaryText.NegativeProbeCount);
            }

            _sampleCount = sampleCount;
        }

        private enum Phase
        {
            LowEndpoint,
            HighEndpoint,
            Bisect,
            Sample,
            Done
        }

        /// <summary>
        /// Gets the index to probe next, or returns false when the search is complete.
        /// </summary>
        public bool TryNextProbe(out long index)
        {
            switch (_phase)
            {
                case Phase.LowEndpoint:
                    index = 0;
                    break;
                case Phase.HighEndpoint:
                    index = _grid.LastIndex;
                    break;
                case Phase.Bisect when _high - _low > 1:
                    index = _low + (_high - _low) / 2;
                    break;
                case Phase.Bisect:
                    _phase = Phase.Sample;
                    _samples = ScheduleSamples();
                    return TryNextProbe(out index);
                case Phase.Sample when _samples != null && _samples.Count > 0:
                    index = _samples.Dequeue();
                    break;
                default:
                    _phase = Phase.Done;
                    index = 0;
                    return false;
            }

            _pending = index;
            return true;
        }

        /// <summary>
        /// Records the verdict for the index returned by the last <see cref="TryNextProbe"/>.
        /// </summary>
        /// <exception cref="RuleAssertionException">Thrown when both endpoints agree (no boundary) or a sample contradicts the located boundary.</exception>
        public void Record(bool isBroken)
        {
            if (_pending == null)
            {
                throw new InvalidOperationException(RuleBoundaryText.NoPendingProbe);
            }

            long index = _pending.Value;
            _pending = null;
            _probed.Add(index);
            _probeCount++;

            switch (_phase)
            {
                case Phase.LowEndpoint:
                    _lowIsBroken = isBroken;
                    _low = 0;
                    _phase = Phase.HighEndpoint;
                    break;
                case Phase.HighEndpoint:
                    if (isBroken == _lowIsBroken)
                    {
                        throw new RuleAssertionException(RuleBoundaryText.NoBoundary(_grid.From, _grid.ValueAt(index), isBroken));
                    }

                    _high = index;
                    _phase = Phase.Bisect;
                    break;
                case Phase.Bisect:
                    if (isBroken == _lowIsBroken)
                    {
                        _low = index;
                    }
                    else
                    {
                        _high = index;
                    }

                    break;
                case Phase.Sample:
                    VerifySample(index, isBroken);
                    break;
                default:
                    throw new InvalidOperationException(RuleBoundaryText.NoPendingProbe);
            }
        }

        /// <summary>
        /// Builds the boundary once the search is complete.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the search is still running.</exception>
        public RuleBoundary<T> Result()
        {
            if (_phase != Phase.Done)
            {
                throw new InvalidOperationException(RuleBoundaryText.SearchNotFinished);
            }

            T lowValue = _grid.ValueAt(_low);
            T highValue = _grid.ValueAt(_high);

            return _lowIsBroken
                ? new RuleBoundary<T>(highValue, lowValue, Monotonicity.BrokenBelow, _probeCount, _grid.Step)
                : new RuleBoundary<T>(lowValue, highValue, Monotonicity.BrokenAbove, _probeCount, _grid.Step);
        }

        private void VerifySample(long index, bool isBroken)
        {
            bool belowBoundary = index <= _low;
            bool expected = belowBoundary ? _lowIsBroken : !_lowIsBroken;

            if (isBroken != expected)
            {
                long neighbour = belowBoundary ? _low : _high;
                Monotonicity monotonicity = _lowIsBroken ? Monotonicity.BrokenBelow : Monotonicity.BrokenAbove;
                throw new RuleAssertionException(RuleBoundaryText.NonMonotone(monotonicity, _grid.ValueAt(index), isBroken, _grid.ValueAt(neighbour)));
            }
        }

        private Queue<long> ScheduleSamples()
        {
            Queue<long> samples = new Queue<long>();
            long interior = _grid.LastIndex - 1;
            long count = Math.Min(_sampleCount, interior);

            if (count <= 0)
            {
                return samples;
            }

            long stride = _grid.LastIndex / (count + 1);
            for (long sample = 1; sample <= count; sample++)
            {
                long index = stride * sample;
                if (index > 0 && index < _grid.LastIndex && !_probed.Contains(index))
                {
                    samples.Enqueue(index);
                }
            }

            return samples;
        }
    }
}
