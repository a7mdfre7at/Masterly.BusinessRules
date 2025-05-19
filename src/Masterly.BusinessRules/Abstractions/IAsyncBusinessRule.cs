using System.Threading;
using System.Threading.Tasks;
using Masterly.BusinessRules.Core;

namespace Masterly.BusinessRules.Abstractions
{
    public interface IAsyncBusinessRule
    {
        string Code { get; }
        string Message { get; }
        RuleSeverity Severity { get; }
        Task<bool> IsBrokenAsync(BusinessRuleContext context, CancellationToken cancellationToken = default);
        Task<BusinessRuleResult?> EvaluateAsync(BusinessRuleContext context, CancellationToken cancellationToken = default);
        Task CheckAsync(BusinessRuleContext context, CancellationToken cancellationToken = default);
    }
}