using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Wraps a context read failure (<see cref="ContextKeyNotFoundException"/> / <see cref="ContextValueTypeMismatchException"/>)
    /// thrown while a checker evaluates a rule into a <see cref="BusinessRuleContextException"/> that names the rule's
    /// <c>Code</c>. Any other exception is returned unchanged, so the checkers can keep rethrowing it with <c>throw;</c>.
    /// The rule's <c>Code</c> getter is read inside a guard so a throwing getter falls back to the rule's type name
    /// instead of masking the original fault.
    /// </summary>
    internal static class ContextReadFault
    {
        /// <summary>
        /// Wraps the exception when it is a context read failure, otherwise returns it unchanged.
        /// </summary>
        public static Exception Wrap(IBusinessRule rule, Exception exception)
        {
            string? key = ContextKeyOf(exception);
            if (key == null)
            {
                return exception;
            }

            return new BusinessRuleContextException(RuleCodeOf(rule), key, exception);
        }

        /// <summary>
        /// Wraps the exception when it is a context read failure, otherwise returns it unchanged.
        /// </summary>
        public static Exception Wrap(IAsyncBusinessRule rule, Exception exception)
        {
            string? key = ContextKeyOf(exception);
            if (key == null)
            {
                return exception;
            }

            return new BusinessRuleContextException(RuleCodeOf(rule), key, exception);
        }

        private static string? ContextKeyOf(Exception exception)
        {
            switch (exception)
            {
                case ContextKeyNotFoundException notFound:
                    return notFound.Key;
                case ContextValueTypeMismatchException mismatch:
                    return mismatch.Key;
                default:
                    return null;
            }
        }

        private static string RuleCodeOf(IBusinessRule rule)
        {
            try
            {
                return rule.Code ?? rule.GetType().Name;
            }
            catch (Exception)
            {
                return rule.GetType().Name;
            }
        }

        private static string RuleCodeOf(IAsyncBusinessRule rule)
        {
            try
            {
                return rule.Code ?? rule.GetType().Name;
            }
            catch (Exception)
            {
                return rule.GetType().Name;
            }
        }
    }
}
