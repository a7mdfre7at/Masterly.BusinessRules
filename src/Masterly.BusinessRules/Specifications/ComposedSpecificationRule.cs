using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// Shared base of the specification combinators (<see cref="AllOfSpecificationRule{TSubject}"/>,
    /// <see cref="AnyOfSpecificationRule{TSubject}"/>, <see cref="NotSpecificationRule{TSubject}"/>). The node keeps its
    /// operands, builds its <see cref="BrokenExpression"/> once by splicing the operands' expressions into a single
    /// lambda (no <see cref="InvocationExpression"/> nodes, so the result stays translatable) and evaluates in memory
    /// through the lazily compiled form of that same lambda, so the in-memory verdict and the translated predicate can
    /// never disagree. It cannot be derived from outside the library.
    /// </summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    public abstract class ComposedSpecificationRule<TSubject> : ISpecificationRule<TSubject>
    {
        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();

        private readonly IReadOnlyList<ISpecificationRule<TSubject>> _operands;
        private readonly Lazy<Expression<Func<TSubject, bool>>> _brokenExpression;
        private readonly Lazy<Expression<Func<TSubject, bool>>> _satisfiedExpression;
        private readonly Lazy<Func<TSubject, bool>> _isBroken;

        private protected ComposedSpecificationRule(RuleOperator @operator, IReadOnlyList<ISpecificationRule<TSubject>> operands)
        {
            Operator = @operator;
            _operands = operands;
            _brokenExpression = new Lazy<Expression<Func<TSubject, bool>>>(BuildBrokenExpression);
            _satisfiedExpression = new Lazy<Expression<Func<TSubject, bool>>>(BuildSatisfiedExpression);
            _isBroken = new Lazy<Func<TSubject, bool>>(() => _brokenExpression.Value.Compile());
        }

        /// <summary>
        /// Gets the synthesised code of the node.
        /// </summary>
        public abstract string Code { get; }

        /// <summary>
        /// Gets the synthesised message of the node.
        /// </summary>
        public abstract string Message { get; }

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Error;

        /// <summary>
        /// Gets the node type name.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets the satisfaction semantics of the node.
        /// </summary>
        public abstract string Description { get; }

        /// <inheritdoc />
        public string Category => string.Empty;

        /// <inheritdoc />
        public IReadOnlyList<string> Tags => EmptyTags;

        /// <summary>
        /// Gets the operator describing how the operands' satisfaction states are combined.
        /// </summary>
        public RuleOperator Operator { get; }

        /// <summary>
        /// Gets the operands in the order they were supplied.
        /// </summary>
        public IReadOnlyList<ISpecificationRule<TSubject>> Operands => _operands;

        /// <summary>
        /// Gets the single lambda selecting the subjects that violate the node. Built once from the operands'
        /// expressions; the same instance is returned on every call.
        /// </summary>
        public Expression<Func<TSubject, bool>> BrokenExpression => _brokenExpression.Value;

        /// <summary>
        /// Gets the single lambda selecting the subjects that comply with the node. Built once; the same instance is
        /// returned on every call.
        /// </summary>
        public Expression<Func<TSubject, bool>> SatisfiedExpression => _satisfiedExpression.Value;

        /// <summary>
        /// Evaluates the compiled <see cref="BrokenExpression"/> against the subject. Compiled once, on the first call.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <returns><c>true</c> if the node is broken for the subject; otherwise, <c>false</c>.</returns>
        public bool IsBroken(TSubject subject) => _isBroken.Value(subject);

        /// <summary>
        /// Evaluates the compiled <see cref="BrokenExpression"/> against the subject. The context is not consulted.
        /// </summary>
        /// <param name="subject">The subject to validate.</param>
        /// <param name="context">The evaluation context; ignored.</param>
        /// <returns><c>true</c> if the node is broken for the subject; otherwise, <c>false</c>.</returns>
        public bool IsBroken(TSubject subject, BusinessRuleContext context) => IsBroken(subject);

        /// <inheritdoc />
        /// <remarks>A factory carrying the node's metadata.</remarks>
        public BusinessRuleResult Evaluate(TSubject subject) => RuleResultFactory.Create(this);

        /// <inheritdoc />
        /// <remarks>The context is not used: a structural node attaches no context-aware details of its own.</remarks>
        public BusinessRuleResult Evaluate(TSubject subject, BusinessRuleContext context) => Evaluate(subject);

        /// <summary>
        /// Builds the node's broken predicate from its operands. Called once.
        /// </summary>
        private protected abstract Expression<Func<TSubject, bool>> BuildBrokenExpression();

        /// <summary>
        /// Builds the node's satisfied predicate. Defaults to the negation of <see cref="BrokenExpression"/>. Called once.
        /// </summary>
        private protected virtual Expression<Func<TSubject, bool>> BuildSatisfiedExpression()
        {
            return SpecificationExpressions.Negate(BrokenExpression);
        }

        /// <summary>
        /// Collects the operands' broken expressions in operand order, rejecting a null expression.
        /// </summary>
        private protected IReadOnlyList<Expression<Func<TSubject, bool>>> BrokenExpressionsOfOperands()
        {
            Expression<Func<TSubject, bool>>[] expressions = new Expression<Func<TSubject, bool>>[_operands.Count];
            for (int index = 0; index < expressions.Length; index++)
            {
                expressions[index] = SpecificationExpressions.RequireBroken(_operands[index]);
            }

            return expressions;
        }

        /// <summary>
        /// Joins the operands' codes with <paramref name="prefix"/>, <see cref="SpecificationText.CodeSeparator"/> and
        /// <see cref="SpecificationText.CodeSuffix"/>, for example <c>ALL(A,B)</c>.
        /// </summary>
        private protected string JoinCodes(string prefix)
        {
            string[] codes = new string[_operands.Count];
            for (int index = 0; index < codes.Length; index++)
            {
                codes[index] = _operands[index].Code;
            }

            return $"{prefix}{string.Join(SpecificationText.CodeSeparator, codes)}{SpecificationText.CodeSuffix}";
        }

        /// <summary>
        /// Joins the operands' messages with <paramref name="prefix"/> and <paramref name="separator"/>.
        /// </summary>
        private protected string JoinMessages(string prefix, string separator)
        {
            string[] messages = new string[_operands.Count];
            for (int index = 0; index < messages.Length; index++)
            {
                messages[index] = _operands[index].Message;
            }

            return $"{prefix}{string.Join(separator, messages)}";
        }

        /// <summary>
        /// Materialises and validates an operand sequence: the sequence and every member must be non-null and the
        /// sequence must not be empty.
        /// </summary>
        /// <param name="operands">The operands to validate.</param>
        /// <param name="parameterName">The caller's parameter name for the exceptions.</param>
        /// <returns>A read-only copy of the operands.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="operands"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the sequence is empty or contains a null operand.</exception>
        private protected static IReadOnlyList<ISpecificationRule<TSubject>> RequireOperands(
            IEnumerable<ISpecificationRule<TSubject>> operands,
            string parameterName)
        {
            if (operands is null)
            {
                throw new ArgumentNullException(parameterName);
            }

            List<ISpecificationRule<TSubject>> copy = new List<ISpecificationRule<TSubject>>(operands);
            if (copy.Count == 0)
            {
                throw new ArgumentException(SpecificationText.EmptyOperands, parameterName);
            }

            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index] is null)
                {
                    throw new ArgumentException(string.Format(SpecificationText.NullOperandFormat, index), parameterName);
                }
            }

            return copy.AsReadOnly();
        }
    }
}
