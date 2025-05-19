using System.Collections.Generic;
using Masterly.BusinessRules.Abstractions;

namespace Masterly.BusinessRules.Core
{
    public abstract class BaseBusinessRule : IBusinessRule
    {
        public abstract string Code { get; }
        public abstract string Message { get; }
        public virtual RuleSeverity Severity => RuleSeverity.Error;

        public void Check()
        {
            if (IsBroken())
                throw new BusinessRuleValidationException(AsEnumerable(Evaluate()));
        }

        public abstract bool IsBroken();

        private IEnumerable<BusinessRuleResult> AsEnumerable(BusinessRuleResult result)
        {
            return new List<BusinessRuleResult> { result };
        }

        public BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, Message, Severity);
    }
}