using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    public abstract class BaseAsyncBusinessRule : IAsyncBusinessRule
    {
        public abstract string Code { get; }
        public abstract string Message { get; }
        public virtual RuleSeverity Severity => RuleSeverity.Error;

        public abstract Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default);

        public virtual async Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            if (await IsBrokenAsync(context, cancellationToken))
                return new BusinessRuleResult(Code, Message, Severity);

            return null;
        }

        public virtual async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            BusinessRuleResult? result = await EvaluateAsync(context, cancellationToken);
            if (result != null)
                throw new BusinessRuleValidationException(new[] { result });
        }
    }
}