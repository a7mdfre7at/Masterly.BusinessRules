using System;
using System.Collections.Generic;
using System.Text;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// The constants and messages of the property sets.
    /// </summary>
    internal static class RulePropertyText
    {
        public const string ContextPureLawName = "ContextPure";
        public const string IterationsNotPositive = "The number of iterations must be greater than zero.";
        public const string NoLaws = "No property was added; call MonotoneIn or ContextPure before Verify.";
        public const string NullContext = "The context factory returned null.";
        public const string UndefinedMonotonicity = "The monotonicity is not a defined value.";
        private const string ListSeparator = ", ";

        public static string MonotoneLawName(Monotonicity monotonicity)
        {
            return $"MonotoneIn({monotonicity})";
        }

        public static string NullRule<T>(T value)
        {
            return $"The rule factory returned null for the value {Format(value)}.";
        }

        public static string NotComparable(Type type)
        {
            return $"{type.Name} is not comparable; pass an IComparer to MonotoneIn.";
        }

        public static string MonotonicityViolated<T>(Monotonicity monotonicity, T brokenAt, T passingAt)
        {
            return $"{MonotoneLawName(monotonicity)} violated: the rule is broken at {Format(brokenAt)} but passing at {Format(passingAt)}.";
        }

        public static string ContextWritten<T>(T value, ContextSnapshot.Difference difference)
        {
            StringBuilder message = new StringBuilder();
            message.Append($"{ContextPureLawName} violated: evaluating the rule for {Format(value)} wrote to the context.");
            AppendKeys(message, "Added", difference.Added);
            AppendKeys(message, "Removed", difference.Removed);
            AppendKeys(message, "Changed", difference.Changed);
            return message.ToString();
        }

        public static string Failure(string lawName, RuleCounterExample counterExample)
        {
            StringBuilder message = new StringBuilder();
            message.Append($"Property '{lawName}' failed. Counter-example: {counterExample.Input} ");
            message.Append($"(seed {Format(counterExample.Seed)}, {Format(counterExample.Shrinks)} shrinks, ");
            message.Append($"{Format(counterExample.Evaluated)} evaluated, {Format(counterExample.Skipped)} skipped); ");
            message.Append($"pass seed: {Format(counterExample.Seed)} to reproduce.");
            message.Append(Environment.NewLine).Append(Fault(counterExample.Failure));
            return message.ToString();
        }

        public static string GeneratorFailed(Exception exception)
        {
            return $"The generator could not produce an input: {exception.Message}";
        }

        public static string Format<T>(T value)
        {
            return FormattableString.Invariant($"{value}");
        }

        private static string Fault(Exception exception)
        {
            return exception is RuleAssertionException ? exception.Message : $"{exception.GetType().Name}: {exception.Message}";
        }

        private static void AppendKeys(StringBuilder message, string label, IReadOnlyList<string> keys)
        {
            if (keys.Count > 0)
            {
                message.Append(' ').Append(label).Append(": ").Append(string.Join(ListSeparator, keys)).Append('.');
            }
        }
    }
}
