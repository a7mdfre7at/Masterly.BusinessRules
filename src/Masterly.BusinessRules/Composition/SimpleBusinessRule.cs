using System;

namespace Masterly.BusinessRules
{
    /// <summary>
    /// A simple business rule implementation that uses a delegate function to determine if the rule is broken.
    /// Useful for creating inline rules without defining a separate class.
    /// </summary>
    /// <example>
    /// <code>
    /// SimpleBusinessRule rule = new SimpleBusinessRule(
    ///     () => age &lt; 18,
    ///     "Must be 18 or older",
    ///     "AGE.INVALID"
    /// );
    /// </code>
    /// </example>
    public class SimpleBusinessRule : BaseBusinessRule
    {
        private readonly Func<bool> _isBrokenFunc;
        private readonly string _message;
        private readonly string _code;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleBusinessRule"/> class.
        /// </summary>
        /// <param name="isBrokenFunc">A function that returns <c>true</c> when the rule is broken.</param>
        /// <param name="message">The message describing why the rule is broken.</param>
        /// <param name="code">The unique code identifying this rule. Defaults to "SIMPLE".</param>
        public SimpleBusinessRule(Func<bool> isBrokenFunc, string message, string code = "SIMPLE")
        {
            _isBrokenFunc = isBrokenFunc;
            _message = message;
            _code = code;
        }

        /// <inheritdoc />
        public override bool IsBroken() => _isBrokenFunc();

        /// <inheritdoc />
        public override string Message => _message;

        /// <inheritdoc />
        public override string Code => _code;
    }
}