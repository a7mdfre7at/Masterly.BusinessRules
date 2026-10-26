using System;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Compiles a textual condition into the asynchronous broken-condition delegate a built async rule evaluates; the
    /// asynchronous twin of <see cref="IRuleConditionCompiler"/>. Compilation itself is synchronous (it is CPU-bound);
    /// the produced delegate is what runs asynchronously. Hand it to
    /// <see cref="AsyncBusinessRuleBuilder.WhenAsync(IAsyncRuleConditionCompiler, string)"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// IAsyncBusinessRule rule = AsyncBusinessRuleBuilder.Create("EMAIL.DUPLICATE")
    ///     .WhenAsync(compiler, "exists(users, email)")
    ///     .Build();
    /// </code>
    /// </example>
    public interface IAsyncRuleConditionCompiler
    {
        /// <summary>
        /// Compiles <paramref name="expression"/> into a delegate that completes with true when the rule is broken for a context.
        /// </summary>
        /// <param name="expression">The condition text.</param>
        /// <returns>The asynchronous broken-condition delegate receiving the context and a cancellation token.</returns>
        Func<BusinessRuleContext, CancellationToken, Task<bool>> Compile(string expression);
    }
}
