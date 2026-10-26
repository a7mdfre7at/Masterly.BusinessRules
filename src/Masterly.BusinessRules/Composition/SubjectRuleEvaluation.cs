using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Shared evaluation logic for the subject-typed And/Or/Not nodes. The subject and, when present, the context are
    /// forwarded to every operand; the binary operators short-circuit exactly like their non-generic twins
    /// (<c>And</c> stops when the first operand passes, <c>Or</c> stops when the first operand is broken).
    /// </summary>
    internal static class SubjectRuleEvaluation
    {
        /// <summary>
        /// Evaluates a typed rule, using the context overload only when a context was supplied.
        /// </summary>
        public static bool IsBroken<TSubject>(IBusinessRule<TSubject> rule, TSubject subject, BusinessRuleContext? context)
        {
            return context == null ? rule.IsBroken(subject) : rule.IsBroken(subject, context);
        }

        /// <summary>
        /// Evaluates a binary node. <paramref name="isOr"/> selects the operator: when the first operand's broken state
        /// equals <paramref name="isOr"/> the verdict is decided and the second operand is not evaluated.
        /// </summary>
        public static bool IsBinaryBroken<TSubject>(bool isOr, IBusinessRule<TSubject> first, IBusinessRule<TSubject> second, TSubject subject, BusinessRuleContext? context)
        {
            bool firstBroken = IsBroken(first, subject, context);
            if (firstBroken == isOr)
            {
                return firstBroken;
            }

            return IsBroken(second, subject, context);
        }

        /// <summary>
        /// Evaluates an async binary node with the same short-circuit rule as <see cref="IsBinaryBroken{TSubject}"/>.
        /// </summary>
        public static async Task<bool> IsBinaryBrokenAsync<TSubject>(
            bool isOr,
            IAsyncBusinessRule<TSubject> first,
            IAsyncBusinessRule<TSubject> second,
            TSubject subject,
            BusinessRuleContext context,
            CancellationToken cancellationToken)
        {
            bool firstBroken = await first.IsBrokenAsync(subject, context, cancellationToken);
            if (firstBroken == isOr)
            {
                return firstBroken;
            }

            return await second.IsBrokenAsync(subject, context, cancellationToken);
        }
    }
}
