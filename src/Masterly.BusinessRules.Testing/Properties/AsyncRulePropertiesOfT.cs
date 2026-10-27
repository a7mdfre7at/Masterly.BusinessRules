using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CsCheck;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// The laws an asynchronous rule family must obey, verified over CsCheck-generated inputs. Each probe builds a fresh
    /// rule with the factory and evaluates <see cref="IAsyncBusinessRule.IsBrokenAsync"/>; <see cref="MonotoneIn"/> uses a
    /// fresh empty context per evaluation, <see cref="ContextPure()"/> the context under observation. Laws are checked in
    /// the order they were added; the first violation stops <see cref="VerifyAsync"/>.
    /// Created through <see cref="RuleProperties.For{T}(Func{T, IAsyncBusinessRule}, Gen{T})"/>.
    /// </summary>
    /// <typeparam name="T">The input type the generator produces and the factory consumes.</typeparam>
    public sealed class AsyncRuleProperties<T>
    {
        private readonly Func<T, IAsyncBusinessRule> _factory;
        private readonly Gen<T> _generator;
        private readonly List<AsyncRulePropertyLaw> _laws = new List<AsyncRulePropertyLaw>();

        internal AsyncRuleProperties(Func<T, IAsyncBusinessRule> factory, Gen<T> generator)
        {
            _factory = factory;
            _generator = generator;
        }

        /// <summary>
        /// Gets the number of laws added so far.
        /// </summary>
        public int LawCount => _laws.Count;

        /// <summary>
        /// Gets the names of the laws added so far, in verification order.
        /// </summary>
        public IReadOnlyList<string> LawNames => RulePropertyLaw.Names(_laws);

        /// <summary>
        /// Adds the law that the rule's broken region is one-sided: for any two inputs <c>a ≤ b</c>, a rule broken at
        /// <c>a</c> is also broken at <c>b</c> (<see cref="Monotonicity.BrokenAbove"/>) or a rule broken at <c>b</c> is also
        /// broken at <c>a</c> (<see cref="Monotonicity.BrokenBelow"/>).
        /// </summary>
        /// <param name="monotonicity">The direction of the broken region.</param>
        /// <param name="comparer">Orders two inputs; null uses <see cref="Comparer{T}.Default"/>, which requires <typeparamref name="T"/> to be comparable.</param>
        /// <returns>This property set.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="monotonicity"/> is undefined.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no comparer is given and <typeparamref name="T"/> is not comparable.</exception>
        public AsyncRuleProperties<T> MonotoneIn(Monotonicity monotonicity, IComparer<T>? comparer = null)
        {
            IComparer<T> ordering = RulePropertyLaw.Ordering(monotonicity, comparer);

            _laws.Add(new SampledAsyncLaw<(T, T)>(
                RulePropertyText.MonotoneLawName(monotonicity),
                Gen.Select(_generator, _generator),
                (pair, cancellationToken) => AssertMonotoneAsync(monotonicity, ordering, pair.Item1, pair.Item2, cancellationToken)));

            return this;
        }

        /// <summary>
        /// Adds the law that evaluating the rule with an empty <see cref="BusinessRuleContext"/> neither adds, removes nor
        /// replaces any key in it, detected through <see cref="BusinessRuleContext.Keys"/> and the stored values.
        /// </summary>
        /// <returns>This property set.</returns>
        public AsyncRuleProperties<T> ContextPure()
        {
            return ContextPure(RulePropertyLaw.EmptyContext);
        }

        /// <summary>
        /// Adds the law that evaluating the rule with a context from <paramref name="contextFactory"/> neither adds,
        /// removes nor replaces any key in it. Mutation of an object already stored in the context is not detectable.
        /// </summary>
        /// <param name="contextFactory">Builds the context for one evaluation; called once per generated input.</param>
        /// <returns>This property set.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="contextFactory"/> is null.</exception>
        public AsyncRuleProperties<T> ContextPure(Func<BusinessRuleContext> contextFactory)
        {
            if (contextFactory == null)
            {
                throw new ArgumentNullException(nameof(contextFactory));
            }

            _laws.Add(new SampledAsyncLaw<T>(
                RulePropertyText.ContextPureLawName,
                _generator,
                (value, cancellationToken) => AssertContextPureAsync(contextFactory, value, cancellationToken)));

            return this;
        }

        /// <summary>
        /// Checks every law against <paramref name="iterations"/> generated inputs. With a <paramref name="seed"/> the
        /// run is fully reproducible; without one a seed is drawn and the failure message names it. A violation is shrunk
        /// to the smallest counter-example CsCheck's size ordering finds within the iterations.
        /// </summary>
        /// <param name="iterations">How many inputs each law is checked against.</param>
        /// <param name="seed">An optional seed making the run deterministic.</param>
        /// <param name="cancellationToken">A token to cancel the verification; checked before every evaluation.</param>
        /// <returns>A task completing when every law held.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="iterations"/> is not positive.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no law was added, a factory returns null, or the generator cannot produce an input.</exception>
        /// <exception cref="RuleAssertionException">Thrown with the shrunk counter-example when a law is violated.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public async Task VerifyAsync(int iterations = RuleProperties.DefaultIterations, int? seed = null, CancellationToken cancellationToken = default)
        {
            RulePropertyLaw.ValidateRun(iterations, _laws);
            int runSeed = RulePropertyLaw.ResolveSeed(seed);

            foreach (AsyncRulePropertyLaw law in _laws)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await law.RunAsync(runSeed, iterations, cancellationToken);
            }
        }

        private async Task<bool> EvaluateAsync(T value, BusinessRuleContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IAsyncBusinessRule rule = _factory(value) ?? throw new RulePropertySetupException(RulePropertyText.NullRule(value));
            return await rule.IsBrokenAsync(context, cancellationToken);
        }

        private async Task AssertMonotoneAsync(Monotonicity monotonicity, IComparer<T> ordering, T first, T second, CancellationToken cancellationToken)
        {
            T lower = ordering.Compare(first, second) <= 0 ? first : second;
            T upper = ordering.Compare(first, second) <= 0 ? second : first;

            bool lowerIsBroken = await EvaluateAsync(lower, new BusinessRuleContext(), cancellationToken);
            bool upperIsBroken = await EvaluateAsync(upper, new BusinessRuleContext(), cancellationToken);

            RulePropertyLaw.ThrowIfNotMonotone(monotonicity, lower, lowerIsBroken, upper, upperIsBroken);
        }

        private async Task AssertContextPureAsync(Func<BusinessRuleContext> contextFactory, T value, CancellationToken cancellationToken)
        {
            BusinessRuleContext context = contextFactory() ?? throw new RulePropertySetupException(RulePropertyText.NullContext);

            ContextSnapshot before = ContextSnapshot.Of(context);
            await EvaluateAsync(value, context, cancellationToken);
            ContextSnapshot after = ContextSnapshot.Of(context);

            RulePropertyLaw.ThrowIfContextWritten(value, before, after);
        }
    }
}
