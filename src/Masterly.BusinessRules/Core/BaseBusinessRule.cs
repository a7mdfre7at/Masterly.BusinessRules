using System.Collections.Generic;

namespace Masterly.BusinessRules
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

        private static IEnumerable<BusinessRuleResult> AsEnumerable(BusinessRuleResult result) => new List<BusinessRuleResult> { result };

        public BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, Message, Severity);
    }
}