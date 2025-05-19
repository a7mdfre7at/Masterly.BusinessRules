using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Masterly.BusinessRules.Abstractions;
using Masterly.BusinessRules.Core;

namespace Masterly.BusinessRules.Composition
{
    public class CompositeAsyncBusinessRule : IAsyncBusinessRule
    {
        private readonly Func<BusinessRuleContext, CancellationToken, Task<bool>> _isBrokenFunc;

        public CompositeAsyncBusinessRule(
            Func<BusinessRuleContext, CancellationToken, Task<bool>> isBrokenFunc,
            string message,
            string code,
            RuleSeverity severity = RuleSeverity.Error)
        {
            _isBrokenFunc = isBrokenFunc;
            Message = message;
            Code = code;
            Severity = severity;
        }

        public string Code { get; }
        public string Message { get; }
        public RuleSeverity Severity { get; }

        public async Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
            => await _isBrokenFunc(context, cancellationToken);

        public async Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            return await IsBrokenAsync(context, cancellationToken)
                ? new BusinessRuleResult(Code, Message, Severity)
                : null;
        }

        public async Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default)
        {
            var result = await EvaluateAsync(context, cancellationToken);
            if (result != null)
                throw new BusinessRuleValidationException(new[] { result });
        }
    }
}