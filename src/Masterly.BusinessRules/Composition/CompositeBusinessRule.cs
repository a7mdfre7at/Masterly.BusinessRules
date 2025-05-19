using System.Collections.Generic;
using System.Linq;
using Masterly.BusinessRules;

namespace Masterly.BusinessRules
{
    public class CompositeBusinessRule : IBusinessRule
    {
        private readonly List<IBusinessRule> _rules = new List<IBusinessRule>();

        public CompositeBusinessRule(IEnumerable<IBusinessRule> rules) => _rules.AddRange(rules);

        public string Code => "CompositeRule";
        public string Message => "One or more business rules failed.";
        public RuleSeverity Severity => RuleSeverity.Error;

        public bool IsBroken() => _rules.Any(r => r.IsBroken());

        public void Check()
        {
            var broken = _rules.Where(r => r.IsBroken()).Select(r => r.Evaluate()).ToList();

            if (broken.Any())
                throw new BusinessRuleValidationException(broken);
        }

        public BusinessRuleResult Evaluate() => new BusinessRuleResult(Code, Message, Severity);

        public IReadOnlyCollection<BusinessRuleResult> EvaluateAll()
            => _rules.Where(r => r.IsBroken()).Select(r => r.Evaluate()).ToList().AsReadOnly();
    }

}
