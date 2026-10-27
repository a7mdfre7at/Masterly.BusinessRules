using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// A textual broken-condition such as <c>amount &gt; @maxOrderAmount &amp;&amp; customer.Tier != 'VIP'</c>, parsed once
    /// into an immutable tree and evaluated against a <see cref="BusinessRuleContext"/> by a single interpreter. The same
    /// walk that produces the verdict can record an explanation with the actual bound values
    /// (<see cref="Explain"/>). Root identifiers bind to context entries, <c>Data</c> to the typed data of a
    /// <see cref="BusinessRuleContext{T}"/>, <c>@name</c> to a rule parameter (declared parameters, then
    /// <see cref="IRuleParameterProvider"/>s, then the context bag). Turn it into a rule with <see cref="ToRule"/> /
    /// <see cref="ToAsyncRule"/>, or into a typed delegate with <see cref="Compile{T}()"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// RuleExpression expression = RuleExpression.Parse("amount > @maxOrderAmount &amp;&amp; customer.Tier != 'VIP'");
    /// ExpressionBusinessRule rule = expression.ToRule("ORDER.TOO_LARGE", "Order exceeds the customer's limit");
    ///
    /// RuleExpressionExplanation why = expression.Explain(context);
    /// // why.Rendered => "(amount(12000) > @maxOrderAmount(10000) [true]) &amp;&amp; (customer.Tier(Gold) != 'VIP' [true]) => true"
    /// </code>
    /// </example>
    public sealed class RuleExpression
    {
        /// <summary>
        /// The root identifier that binds to the typed data of a <see cref="BusinessRuleContext{T}"/>.
        /// </summary>
        public const string DataIdentifier = "Data";

        /// <summary>
        /// The context key under which <see cref="BusinessRuleContext{T}"/> stores its data and to which
        /// <see cref="DataIdentifier"/> binds.
        /// </summary>
        public const string TypedDataKey = "__typedData";

        private RuleExpression(string text, RuleExpressionOptions options, RuleExpressionNode root)
        {
            Text = text;
            Options = options;
            Root = root;
            RuleExpressionNameCollector.Collect(root, out IReadOnlyList<string> identifiers, out IReadOnlyList<string> parameters);
            Identifiers = identifiers;
            ParameterNames = parameters;
        }

        /// <summary>
        /// Gets the source text as given to <see cref="Parse(string, RuleExpressionOptions)"/>.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the options the expression was parsed with; they also govern member access and explanation formatting.
        /// </summary>
        public RuleExpressionOptions Options { get; }

        /// <summary>
        /// Gets the distinct root identifiers (context keys, including <see cref="DataIdentifier"/> when used) in order of first appearance.
        /// </summary>
        public IReadOnlyList<string> Identifiers { get; }

        /// <summary>
        /// Gets the distinct <c>@parameter</c> names, without the <c>@</c>, in order of first appearance.
        /// </summary>
        public IReadOnlyList<string> ParameterNames { get; }

        /// <summary>
        /// Gets the nesting depth of the parsed tree.
        /// </summary>
        public int Depth => Root.Depth;

        internal RuleExpressionNode Root { get; }

        /// <summary>
        /// Parses <paramref name="text"/> with <see cref="RuleExpressionOptions.Default"/>.
        /// </summary>
        /// <param name="text">The expression text.</param>
        /// <returns>The parsed expression.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        /// <exception cref="RuleExpressionParseException">Thrown when the text is not a valid expression or exceeds a parse limit.</exception>
        public static RuleExpression Parse(string text) => Parse(text, RuleExpressionOptions.Default);

        /// <summary>
        /// Parses <paramref name="text"/> with <paramref name="options"/>.
        /// </summary>
        /// <param name="text">The expression text.</param>
        /// <param name="options">The parse and evaluation options.</param>
        /// <returns>The parsed expression.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="RuleExpressionParseException">Thrown when the text is not a valid expression or exceeds a parse limit.</exception>
        public static RuleExpression Parse(string text, RuleExpressionOptions options)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return new RuleExpression(text, options, RuleExpressionParser.Parse(text, options));
        }

        /// <summary>
        /// Tries to parse <paramref name="text"/>; never throws for invalid text.
        /// </summary>
        /// <param name="text">The expression text.</param>
        /// <param name="options">The parse and evaluation options, or null for <see cref="RuleExpressionOptions.Default"/>.</param>
        /// <param name="expression">The parsed expression, or null when the method returns false.</param>
        /// <returns>True when the text parsed.</returns>
        public static bool TryParse(string? text, RuleExpressionOptions? options, out RuleExpression? expression)
        {
            if (text == null)
            {
                expression = null;
                return false;
            }

            try
            {
                expression = Parse(text, options ?? RuleExpressionOptions.Default);
                return true;
            }
            catch (RuleExpressionParseException)
            {
                expression = null;
                return false;
            }
        }

        /// <summary>
        /// Evaluates the expression against <paramref name="context"/>: true means the broken condition holds.
        /// </summary>
        /// <param name="context">The context whose entries the identifiers bind to.</param>
        /// <param name="parameters">An optional provider for <c>@parameters</c>, consulted after a provider stored under <see cref="RuleParameters.ContextKey"/> and before the context bag.</param>
        /// <returns>The verdict.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
        /// <exception cref="RuleExpressionBindingException">Thrown when an identifier, parameter or member cannot be bound.</exception>
        /// <exception cref="RuleExpressionEvaluationException">Thrown when an operator meets operands it cannot work on.</exception>
        public bool Evaluate(BusinessRuleContext context, IRuleParameterProvider? parameters = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return EvaluateCore(context, RuleExpressionParameterSource.ForSync(context, null, parameters));
        }

        /// <summary>
        /// Evaluates the expression against <paramref name="context"/>, resolving <c>@parameters</c> through asynchronous
        /// providers first (<see cref="RuleParameters.AsyncContextKey"/>, then <paramref name="asyncParameters"/>) and
        /// synchronous ones second (<see cref="RuleParameters.ContextKey"/>, then <paramref name="parameters"/>).
        /// </summary>
        /// <param name="context">The context whose entries the identifiers bind to.</param>
        /// <param name="asyncParameters">An optional asynchronous provider for <c>@parameters</c>.</param>
        /// <param name="parameters">An optional synchronous provider for <c>@parameters</c>.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task producing the verdict.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
        /// <exception cref="RuleExpressionBindingException">Thrown when an identifier, parameter or member cannot be bound.</exception>
        /// <exception cref="RuleExpressionEvaluationException">Thrown when an operator meets operands it cannot work on.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public Task<bool> EvaluateAsync(BusinessRuleContext context, IAsyncRuleParameterProvider? asyncParameters = null, IRuleParameterProvider? parameters = null, CancellationToken cancellationToken = default)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return EvaluateAsyncCore(context, null, asyncParameters, parameters, cancellationToken);
        }

        /// <summary>
        /// Evaluates the expression once and explains the verdict with the actual bound values.
        /// </summary>
        /// <param name="context">The context whose entries the identifiers bind to.</param>
        /// <param name="redact">An optional delegate deciding how each bound value is displayed, for masking PII.</param>
        /// <param name="parameters">An optional provider for <c>@parameters</c>.</param>
        /// <returns>The explanation: verdict, steps and rendered line.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
        /// <exception cref="RuleExpressionBindingException">Thrown when an identifier, parameter or member cannot be bound.</exception>
        /// <exception cref="RuleExpressionEvaluationException">Thrown when an operator meets operands it cannot work on.</exception>
        public RuleExpressionExplanation Explain(BusinessRuleContext context, RuleExpressionRedactor? redact = null, IRuleParameterProvider? parameters = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return ExplainCore(context, RuleExpressionParameterSource.ForSync(context, null, parameters), redact);
        }

        /// <summary>
        /// Evaluates the expression once, resolving <c>@parameters</c> as <see cref="EvaluateAsync"/> does, and explains
        /// the verdict with the actual bound values.
        /// </summary>
        /// <param name="context">The context whose entries the identifiers bind to.</param>
        /// <param name="redact">An optional delegate deciding how each bound value is displayed, for masking PII.</param>
        /// <param name="asyncParameters">An optional asynchronous provider for <c>@parameters</c>.</param>
        /// <param name="parameters">An optional synchronous provider for <c>@parameters</c>.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task producing the explanation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public async Task<RuleExpressionExplanation> ExplainAsync(
            BusinessRuleContext context,
            RuleExpressionRedactor? redact = null,
            IAsyncRuleParameterProvider? asyncParameters = null,
            IRuleParameterProvider? parameters = null,
            CancellationToken cancellationToken = default)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return await ExplainAsyncCore(context, null, asyncParameters, parameters, redact, cancellationToken);
        }

        /// <summary>
        /// Wraps the expression in a synchronous rule.
        /// </summary>
        /// <param name="code">The rule code.</param>
        /// <param name="message">The violation message.</param>
        /// <param name="parameters">An optional provider for <c>@parameters</c>.</param>
        /// <param name="severity">The rule severity; <see cref="RuleSeverity.Error"/> by default.</param>
        /// <returns>The rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> or <paramref name="message"/> is null.</exception>
        public ExpressionBusinessRule ToRule(string code, string message, IRuleParameterProvider? parameters = null, RuleSeverity severity = RuleSeverity.Error)
            => new ExpressionBusinessRule(this, code, message, parameters, severity);

        /// <summary>
        /// Wraps the expression in an asynchronous rule.
        /// </summary>
        /// <param name="code">The rule code.</param>
        /// <param name="message">The violation message.</param>
        /// <param name="asyncParameters">An optional asynchronous provider for <c>@parameters</c>.</param>
        /// <param name="parameters">An optional synchronous provider for <c>@parameters</c>.</param>
        /// <param name="severity">The rule severity; <see cref="RuleSeverity.Error"/> by default.</param>
        /// <returns>The rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> or <paramref name="message"/> is null.</exception>
        public ExpressionAsyncBusinessRule ToAsyncRule(string code, string message, IAsyncRuleParameterProvider? asyncParameters = null, IRuleParameterProvider? parameters = null, RuleSeverity severity = RuleSeverity.Error)
            => new ExpressionAsyncBusinessRule(this, code, message, asyncParameters, parameters, severity);

        /// <summary>
        /// Compiles the expression for a <see cref="BusinessRuleContext{T}"/>: paths rooted at <c>Data</c> become
        /// statically resolved member accesses, so a misspelt member fails here rather than at evaluation time. Other
        /// identifiers still bind to the context bag at evaluation time. Requires dynamic code generation; prefer the
        /// interpreter (<see cref="Evaluate"/>) under AOT.
        /// </summary>
        /// <typeparam name="T">The subject type of the typed context.</typeparam>
        /// <returns>The compiled broken-condition delegate.</returns>
        /// <exception cref="RuleExpressionParseException">Thrown when a <c>Data</c> member does not exist on <typeparamref name="T"/> or is not readable under the options.</exception>
        public Func<BusinessRuleContext<T>, bool> Compile<T>() where T : class => Compile<T>(null);

        /// <summary>
        /// Compiles the expression for a <see cref="BusinessRuleContext{T}"/> with a provider for <c>@parameters</c>.
        /// </summary>
        /// <typeparam name="T">The subject type of the typed context.</typeparam>
        /// <param name="parameters">An optional provider for <c>@parameters</c>.</param>
        /// <returns>The compiled broken-condition delegate.</returns>
        /// <exception cref="RuleExpressionParseException">Thrown when a <c>Data</c> member does not exist on <typeparamref name="T"/> or is not readable under the options.</exception>
        public Func<BusinessRuleContext<T>, bool> Compile<T>(IRuleParameterProvider? parameters) where T : class
            => RuleExpressionTypedCompiler<T>.Compile(Root, Options, parameters, ParameterNames.Count > 0);

        /// <summary>
        /// Returns the source text.
        /// </summary>
        public override string ToString() => Text;

        internal bool EvaluateCore(BusinessRuleContext context, RuleExpressionParameterSource parameters)
        {
            RuleExpressionInterpreter interpreter = new RuleExpressionInterpreter(context, parameters, Options, null);
            return interpreter.EvaluateRoot(Root);
        }

        internal async Task<bool> EvaluateAsyncCore(
            BusinessRuleContext context,
            RuleParameters? declared,
            IAsyncRuleParameterProvider? asyncParameters,
            IRuleParameterProvider? parameters,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RuleExpressionParameterSource source = await RuleExpressionParameterSource.ForAsync(context, ParameterNames, declared, asyncParameters, parameters, cancellationToken);
            return EvaluateCore(context, source);
        }

        internal async Task<RuleExpressionExplanation> ExplainAsyncCore(
            BusinessRuleContext context,
            RuleParameters? declared,
            IAsyncRuleParameterProvider? asyncParameters,
            IRuleParameterProvider? parameters,
            RuleExpressionRedactor? redact,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RuleExpressionParameterSource source = await RuleExpressionParameterSource.ForAsync(context, ParameterNames, declared, asyncParameters, parameters, cancellationToken);
            return ExplainCore(context, source, redact);
        }

        internal RuleExpressionExplanation ExplainCore(BusinessRuleContext context, RuleExpressionParameterSource parameters, RuleExpressionRedactor? redact)
        {
            RuleExpressionBindingRecorder recorder = new RuleExpressionBindingRecorder(redact);
            RuleExpressionInterpreter interpreter = new RuleExpressionInterpreter(context, parameters, Options, recorder);
            bool result = interpreter.EvaluateRoot(Root);
            string rendered = RuleExpressionExplanationRenderer.Render(Root, result, recorder, Options.Culture);
            return new RuleExpressionExplanation(this, result, recorder.Steps, rendered);
        }
    }
}
