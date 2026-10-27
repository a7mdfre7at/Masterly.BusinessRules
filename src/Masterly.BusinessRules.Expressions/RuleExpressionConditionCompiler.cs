using System;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// The core condition-compiler seam (<see cref="IRuleConditionCompiler"/> and <see cref="IAsyncRuleConditionCompiler"/>)
    /// implemented with <see cref="RuleExpression"/>: hand an instance to <c>BusinessRuleBuilder.When(compiler, text)</c>
    /// or <c>AsyncBusinessRuleBuilder.WhenAsync(compiler, text)</c>, or register it in a container so rules can be loaded
    /// from configuration. A malformed expression fails at compile (builder) time with <see cref="RuleExpressionParseException"/>.
    /// Because both seam interfaces declare <c>Compile(string)</c> with different return types, the asynchronous one is
    /// implemented explicitly and also exposed as <see cref="CompileAsyncCondition"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// RuleExpressionConditionCompiler compiler = new RuleExpressionConditionCompiler(RuleExpressionOptions.Default, parameters);
    ///
    /// IBusinessRule rule = BusinessRuleBuilder.Create("ORDER.TOO_LARGE").When(compiler, "amount > @maxOrderAmount").Build();
    /// IAsyncBusinessRule asyncRule = AsyncBusinessRuleBuilder.Create("ORDER.TOO_LARGE").WhenAsync(compiler, "amount > @maxOrderAmount").Build();
    /// </code>
    /// </example>
    public sealed class RuleExpressionConditionCompiler : IRuleConditionCompiler, IAsyncRuleConditionCompiler
    {
        /// <summary>
        /// Creates a compiler with <see cref="RuleExpressionOptions.Default"/> and no injected parameter providers.
        /// </summary>
        public RuleExpressionConditionCompiler()
            : this(RuleExpressionOptions.Default)
        {
        }

        /// <summary>
        /// Creates a compiler.
        /// </summary>
        /// <param name="options">The parse and evaluation options.</param>
        /// <param name="parameters">An optional synchronous provider for <c>@parameters</c>.</param>
        /// <param name="asyncParameters">An optional asynchronous provider for <c>@parameters</c>, used by the async delegates only.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public RuleExpressionConditionCompiler(RuleExpressionOptions options, IRuleParameterProvider? parameters = null, IAsyncRuleParameterProvider? asyncParameters = null)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Parameters = parameters;
            AsyncParameters = asyncParameters;
        }

        /// <summary>
        /// Gets the parse and evaluation options.
        /// </summary>
        public RuleExpressionOptions Options { get; }

        /// <summary>
        /// Gets the synchronous provider for <c>@parameters</c>, or null.
        /// </summary>
        public IRuleParameterProvider? Parameters { get; }

        /// <summary>
        /// Gets the asynchronous provider for <c>@parameters</c>, or null.
        /// </summary>
        public IAsyncRuleParameterProvider? AsyncParameters { get; }

        /// <summary>
        /// Parses <paramref name="expression"/> and returns the synchronous broken-condition delegate.
        /// </summary>
        /// <param name="expression">The condition text.</param>
        /// <returns>The delegate evaluating the expression against a context.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> is null.</exception>
        /// <exception cref="RuleExpressionParseException">Thrown when the text is not a valid expression.</exception>
        public Func<BusinessRuleContext, bool> Compile(string expression)
        {
            RuleExpression parsed = RuleExpression.Parse(expression, Options);
            IRuleParameterProvider? parameters = Parameters;
            return context => parsed.Evaluate(context, parameters);
        }

        /// <summary>
        /// Parses <paramref name="expression"/> and returns the asynchronous broken-condition delegate.
        /// </summary>
        /// <param name="expression">The condition text.</param>
        /// <returns>The delegate evaluating the expression against a context with a cancellation token.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> is null.</exception>
        /// <exception cref="RuleExpressionParseException">Thrown when the text is not a valid expression.</exception>
        public Func<BusinessRuleContext, CancellationToken, Task<bool>> CompileAsyncCondition(string expression)
        {
            RuleExpression parsed = RuleExpression.Parse(expression, Options);
            IAsyncRuleParameterProvider? asyncParameters = AsyncParameters;
            IRuleParameterProvider? parameters = Parameters;
            return (context, cancellationToken) => parsed.EvaluateAsync(context, asyncParameters, parameters, cancellationToken);
        }

        Func<BusinessRuleContext, CancellationToken, Task<bool>> IAsyncRuleConditionCompiler.Compile(string expression) => CompileAsyncCondition(expression);
    }
}
