using System;
using System.Threading;
using System.Threading.Tasks;
using CsCheck;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// Runs a law over CsCheck-generated inputs with CsCheck's own random-shrinking strategy, single-threaded (rules and
    /// contexts are not thread-safe) and driven by one <see cref="PCG"/> seeded from the caller's integer seed, so a run
    /// with the same seed is fully reproducible. Once a counter-example is found, later inputs are only evaluated when
    /// their CsCheck <see cref="Size"/> is smaller, so the reported counter-example is the smallest one seen.
    /// <see cref="Check.Sample{T}(Gen{T}, Action{T}, Action{string}, string, long, int, int, Func{T, string}, ILogger)"/>
    /// is not used because its seed only fixes the first iteration and it hides the original exception.
    /// Cancellation surfaces as <see cref="OperationCanceledException"/> and a misconfigured set (including a generator
    /// that cannot produce a value) as <see cref="RulePropertySetupException"/>, never as a failed law.
    /// </summary>
    internal static class RulePropertySampler
    {
        private const uint SeedStream = 0u;

        /// <summary>
        /// Draws a seed for a run that was started without one, so the failure message can name it.
        /// </summary>
        public static int RandomSeed()
        {
            return unchecked((int)PCG.ThreadPCG.Next());
        }

        public static void Sample<TInput>(string lawName, Gen<TInput> generator, Action<TInput> assert, int seed, int iterations)
        {
            PCG random = CreateRandom(seed);
            SampleProgress<TInput> progress = new SampleProgress<TInput>();

            try
            {
                for (int iteration = 0; iteration < iterations; iteration++)
                {
                    if (progress.TryNext(generator, random, out TInput input, out Size size))
                    {
                        AssertOne(progress, assert, input, size);
                    }
                }
            }
            catch (RulePropertySetupException setup)
            {
                throw setup.ToPublicException();
            }

            progress.ThrowIfFailed(lawName, seed);
        }

        public static async Task SampleAsync<TInput>(
            string lawName,
            Gen<TInput> generator,
            Func<TInput, CancellationToken, Task> assert,
            int seed,
            int iterations,
            CancellationToken cancellationToken)
        {
            PCG random = CreateRandom(seed);
            SampleProgress<TInput> progress = new SampleProgress<TInput>();

            try
            {
                for (int iteration = 0; iteration < iterations; iteration++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (progress.TryNext(generator, random, out TInput input, out Size size))
                    {
                        await AssertOneAsync(progress, assert, input, size, cancellationToken);
                    }
                }
            }
            catch (RulePropertySetupException setup)
            {
                throw setup.ToPublicException();
            }

            progress.ThrowIfFailed(lawName, seed);
        }

        private static void AssertOne<TInput>(SampleProgress<TInput> progress, Action<TInput> assert, TInput input, Size size)
        {
            try
            {
                assert(input);
            }
            catch (Exception exception) when (IsCounterExample(exception))
            {
                progress.Fail(input, size, exception);
            }
        }

        private static async Task AssertOneAsync<TInput>(
            SampleProgress<TInput> progress,
            Func<TInput, CancellationToken, Task> assert,
            TInput input,
            Size size,
            CancellationToken cancellationToken)
        {
            try
            {
                await assert(input, cancellationToken);
            }
            catch (Exception exception) when (IsCounterExample(exception))
            {
                progress.Fail(input, size, exception);
            }
        }

        private static PCG CreateRandom(int seed)
        {
            return new PCG(SeedStream, unchecked((ulong)(uint)seed));
        }

        private static bool IsCounterExample(Exception exception)
        {
            return !(exception is RulePropertySetupException) && !(exception is OperationCanceledException);
        }

        /// <summary>
        /// The state of one sampled law: the smallest counter-example so far and how many inputs were evaluated or skipped.
        /// </summary>
        private sealed class SampleProgress<TInput>
        {
            private Size? _smallestSize;
            private TInput _smallestInput = default!; // Only read once _failure is set, which also assigns this field.
            private Exception? _failure;
            private int _shrinks;
            private int _evaluated;
            private int _skipped;

            /// <summary>
            /// Generates the next input; returns false (and counts a skip) when a counter-example is already known and
            /// the input is not smaller than it.
            /// </summary>
            public bool TryNext(Gen<TInput> generator, PCG random, out TInput input, out Size size)
            {
                input = Generate(generator, random, _smallestSize, out size);

                if (_smallestSize != null && !Size.IsLessThan(size, _smallestSize))
                {
                    _skipped++;
                    return false;
                }

                _evaluated++;
                return true;
            }

            public void Fail(TInput input, Size size, Exception exception)
            {
                if (_failure != null)
                {
                    _shrinks++;
                }

                _smallestSize = size;
                _smallestInput = input;
                _failure = exception;
            }

            public void ThrowIfFailed(string lawName, int seed)
            {
                if (_failure != null)
                {
                    RuleCounterExample counterExample = new RuleCounterExample(
                        RulePropertyText.Format(_smallestInput),
                        _failure,
                        seed,
                        _shrinks,
                        _evaluated,
                        _skipped);

                    throw new RuleAssertionException(RulePropertyText.Failure(lawName, counterExample));
                }
            }

            private static TInput Generate(Gen<TInput> generator, PCG random, Size? smallest, out Size size)
            {
                try
                {
                    return generator.Generate(random, smallest, out size);
                }
                catch (Exception exception) when (!(exception is OperationCanceledException))
                {
                    throw new RulePropertySetupException(RulePropertyText.GeneratorFailed(exception), exception);
                }
            }
        }
    }
}
