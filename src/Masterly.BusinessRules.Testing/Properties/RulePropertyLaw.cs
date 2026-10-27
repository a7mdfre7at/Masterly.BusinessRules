using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CsCheck;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// A named law of either property set.
    /// </summary>
    internal interface IRulePropertyLaw
    {
        string Name { get; }
    }

    /// <summary>
    /// One named law of a synchronous property set, plus the helpers both property sets share: argument validation,
    /// seed resolution, the monotonicity and context-purity verdicts.
    /// </summary>
    internal abstract class RulePropertyLaw : IRulePropertyLaw
    {
        protected RulePropertyLaw(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public abstract void Run(int seed, int iterations);

        public static BusinessRuleContext EmptyContext()
        {
            return new BusinessRuleContext();
        }

        public static IReadOnlyList<string> Names(IReadOnlyList<IRulePropertyLaw> laws)
        {
            List<string> names = new List<string>(laws.Count);
            foreach (IRulePropertyLaw law in laws)
            {
                names.Add(law.Name);
            }

            return names;
        }

        public static void ValidateRun(int iterations, IReadOnlyList<IRulePropertyLaw> laws)
        {
            if (iterations <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(iterations), iterations, RulePropertyText.IterationsNotPositive);
            }

            if (laws.Count == 0)
            {
                throw new InvalidOperationException(RulePropertyText.NoLaws);
            }
        }

        /// <summary>
        /// Returns the caller's seed, or draws one when none was given so the failure message can name it.
        /// </summary>
        public static int ResolveSeed(int? seed)
        {
            return seed ?? RulePropertySampler.RandomSeed();
        }

        public static IComparer<T> Ordering<T>(Monotonicity monotonicity, IComparer<T>? comparer)
        {
            if (!Enum.IsDefined(typeof(Monotonicity), monotonicity))
            {
                throw new ArgumentOutOfRangeException(nameof(monotonicity), monotonicity, RulePropertyText.UndefinedMonotonicity);
            }

            if (comparer != null)
            {
                return comparer;
            }

            bool comparable = typeof(IComparable<T>).IsAssignableFrom(typeof(T)) || typeof(IComparable).IsAssignableFrom(typeof(T));
            if (!comparable)
            {
                throw new InvalidOperationException(RulePropertyText.NotComparable(typeof(T)));
            }

            return Comparer<T>.Default;
        }

        public static void ThrowIfNotMonotone<T>(Monotonicity monotonicity, T lower, bool lowerIsBroken, T upper, bool upperIsBroken)
        {
            if (monotonicity == Monotonicity.BrokenAbove && lowerIsBroken && !upperIsBroken)
            {
                throw new RuleAssertionException(RulePropertyText.MonotonicityViolated(monotonicity, lower, upper));
            }

            if (monotonicity == Monotonicity.BrokenBelow && upperIsBroken && !lowerIsBroken)
            {
                throw new RuleAssertionException(RulePropertyText.MonotonicityViolated(monotonicity, upper, lower));
            }
        }

        public static void ThrowIfContextWritten<T>(T value, ContextSnapshot before, ContextSnapshot after)
        {
            ContextSnapshot.Difference difference = before.DiffTo(after);
            if (!difference.IsEmpty)
            {
                throw new RuleAssertionException(RulePropertyText.ContextWritten(value, difference));
            }
        }
    }

    /// <summary>
    /// One named law of an asynchronous property set.
    /// </summary>
    internal abstract class AsyncRulePropertyLaw : IRulePropertyLaw
    {
        protected AsyncRulePropertyLaw(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public abstract Task RunAsync(int seed, int iterations, CancellationToken cancellationToken);
    }

    /// <summary>
    /// A synchronous law sampled by CsCheck: the assertion throws for a violating input and CsCheck shrinks it.
    /// </summary>
    internal sealed class SampledLaw<TInput> : RulePropertyLaw
    {
        private readonly Gen<TInput> _generator;
        private readonly Action<TInput> _assert;

        public SampledLaw(string name, Gen<TInput> generator, Action<TInput> assert) : base(name)
        {
            _generator = generator;
            _assert = assert;
        }

        public override void Run(int seed, int iterations)
        {
            RulePropertySampler.Sample(Name, _generator, _assert, seed, iterations);
        }
    }

    /// <summary>
    /// An asynchronous law sampled by CsCheck: the assertion throws for a violating input and CsCheck shrinks it.
    /// </summary>
    internal sealed class SampledAsyncLaw<TInput> : AsyncRulePropertyLaw
    {
        private readonly Gen<TInput> _generator;
        private readonly Func<TInput, CancellationToken, Task> _assert;

        public SampledAsyncLaw(string name, Gen<TInput> generator, Func<TInput, CancellationToken, Task> assert) : base(name)
        {
            _generator = generator;
            _assert = assert;
        }

        public override Task RunAsync(int seed, int iterations, CancellationToken cancellationToken)
        {
            return RulePropertySampler.SampleAsync(Name, _generator, _assert, seed, iterations, cancellationToken);
        }
    }
}
