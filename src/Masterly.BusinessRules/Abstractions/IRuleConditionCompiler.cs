using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Compiles a textual condition (for example <c>amount &gt; @maxOrderAmount</c>) into the synchronous broken-condition
    /// delegate a built rule evaluates. This is the seam an expression package plugs into: core never parses text, it only
    /// hands the compiled delegate to <see cref="BusinessRuleBuilder.When(IRuleConditionCompiler, string)"/>. Compilation
    /// happens when the builder method is called, so a malformed expression fails at build time, not at evaluation time.
    /// </summary>
    /// <example>
    /// <code>
    /// IBusinessRule rule = BusinessRuleBuilder.Create("ORDER.TOO_LARGE")
    ///     .When(compiler, "amount > 10000")
    ///     .Build();
    /// </code>
    /// </example>
    public interface IRuleConditionCompiler
    {
        /// <summary>
        /// Compiles <paramref name="expression"/> into a delegate that returns true when the rule is broken for a context.
        /// </summary>
        /// <param name="expression">The condition text.</param>
        /// <returns>The broken-condition delegate.</returns>
        Func<BusinessRuleContext, bool> Compile(string expression);
    }
}
