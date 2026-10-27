using System;
using System.Globalization;
using System.Reflection;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// Immutable parse and evaluation settings for <see cref="RuleExpression"/>: the parse limits
    /// (<see cref="MaxLength"/>, <see cref="MaxDepth"/>), which members an expression may read (<see cref="MemberFilter"/>,
    /// <see cref="RequireRuleAccessibleAttribute"/>) and the <see cref="Culture"/> used to format bound values in
    /// explanations. Start from <see cref="Default"/> and derive with the <c>With...</c> methods.
    /// </summary>
    /// <example>
    /// <code>
    /// RuleExpressionOptions options = RuleExpressionOptions.Default
    ///     .WithMaxLength(500)
    ///     .WithRequireRuleAccessibleAttribute(true);
    ///
    /// RuleExpression expression = RuleExpression.Parse("Data.Items.Count == 0", options);
    /// </code>
    /// </example>
    public sealed class RuleExpressionOptions
    {
        /// <summary>
        /// The default <see cref="MaxLength"/>: 4096 characters.
        /// </summary>
        public const int DefaultMaxLength = 4096;

        /// <summary>
        /// The default <see cref="MaxDepth"/>: 64 nested levels.
        /// </summary>
        public const int DefaultMaxDepth = 64;

        private static readonly RuleExpressionOptions DefaultOptions = new RuleExpressionOptions(DefaultMaxLength, DefaultMaxDepth, null, false, CultureInfo.InvariantCulture);

        private RuleExpressionOptions(int maxLength, int maxDepth, Func<PropertyInfo, bool>? memberFilter, bool requireRuleAccessibleAttribute, CultureInfo culture)
        {
            MaxLength = maxLength;
            MaxDepth = maxDepth;
            MemberFilter = memberFilter;
            RequireRuleAccessibleAttribute = requireRuleAccessibleAttribute;
            Culture = culture;
        }

        /// <summary>
        /// Gets the default options: 4096 characters, 64 levels, every public instance property readable, no attribute
        /// required, invariant culture.
        /// </summary>
        public static RuleExpressionOptions Default => DefaultOptions;

        /// <summary>
        /// Gets the maximum number of characters an expression may have; longer text fails to parse.
        /// </summary>
        public int MaxLength { get; }

        /// <summary>
        /// Gets the maximum nesting depth of the parsed tree (parentheses, operators, member chains and lists all
        /// count); deeper text fails to parse.
        /// </summary>
        public int MaxDepth { get; }

        /// <summary>
        /// Gets the predicate deciding which public instance properties an expression may read, or null to allow all of
        /// them. The predicate is applied after the built-in restriction to public instance properties and after
        /// <see cref="RequireRuleAccessibleAttribute"/>.
        /// </summary>
        public Func<PropertyInfo, bool>? MemberFilter { get; }

        /// <summary>
        /// Gets whether a property is readable only when it, or its declaring type, carries <see cref="RuleAccessibleAttribute"/>.
        /// <c>Count</c> and <c>Length</c> on collections and strings stay readable regardless.
        /// </summary>
        public bool RequireRuleAccessibleAttribute { get; }

        /// <summary>
        /// Gets the culture used to format bound values in <see cref="RuleExpressionExplanation.Rendered"/>. Literals are
        /// always parsed with the invariant culture.
        /// </summary>
        public CultureInfo Culture { get; }

        /// <summary>
        /// Returns a copy with another <see cref="MaxLength"/>.
        /// </summary>
        /// <param name="maxLength">The maximum number of characters; at least 1.</param>
        /// <returns>The derived options.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxLength"/> is less than 1.</exception>
        public RuleExpressionOptions WithMaxLength(int maxLength)
        {
            if (maxLength < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLength), maxLength, "The maximum length must be at least 1.");
            }

            return new RuleExpressionOptions(maxLength, MaxDepth, MemberFilter, RequireRuleAccessibleAttribute, Culture);
        }

        /// <summary>
        /// Returns a copy with another <see cref="MaxDepth"/>.
        /// </summary>
        /// <param name="maxDepth">The maximum nesting depth; at least 1.</param>
        /// <returns>The derived options.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxDepth"/> is less than 1.</exception>
        public RuleExpressionOptions WithMaxDepth(int maxDepth)
        {
            if (maxDepth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "The maximum depth must be at least 1.");
            }

            return new RuleExpressionOptions(MaxLength, maxDepth, MemberFilter, RequireRuleAccessibleAttribute, Culture);
        }

        /// <summary>
        /// Returns a copy with another <see cref="MemberFilter"/>.
        /// </summary>
        /// <param name="memberFilter">The predicate, or null to allow every public instance property.</param>
        /// <returns>The derived options.</returns>
        public RuleExpressionOptions WithMemberFilter(Func<PropertyInfo, bool>? memberFilter)
            => new RuleExpressionOptions(MaxLength, MaxDepth, memberFilter, RequireRuleAccessibleAttribute, Culture);

        /// <summary>
        /// Returns a copy with another <see cref="RequireRuleAccessibleAttribute"/> setting.
        /// </summary>
        /// <param name="require">True to read only properties marked <see cref="RuleAccessibleAttribute"/>.</param>
        /// <returns>The derived options.</returns>
        public RuleExpressionOptions WithRequireRuleAccessibleAttribute(bool require)
            => new RuleExpressionOptions(MaxLength, MaxDepth, MemberFilter, require, Culture);

        /// <summary>
        /// Returns a copy with another <see cref="Culture"/>.
        /// </summary>
        /// <param name="culture">The culture used to format bound values in explanations.</param>
        /// <returns>The derived options.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="culture"/> is null.</exception>
        public RuleExpressionOptions WithCulture(CultureInfo culture)
        {
            if (culture == null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            return new RuleExpressionOptions(MaxLength, MaxDepth, MemberFilter, RequireRuleAccessibleAttribute, culture);
        }

        /// <summary>
        /// Decides whether an expression may read <paramref name="property"/> under these options.
        /// </summary>
        internal bool IsAccessible(PropertyInfo property)
        {
            MethodInfo? getter = property.GetMethod;
            if (getter == null || !getter.IsPublic || getter.IsStatic || property.GetIndexParameters().Length > 0)
            {
                return false;
            }

            if (RequireRuleAccessibleAttribute && !HasRuleAccessibleAttribute(property))
            {
                return false;
            }

            return MemberFilter == null || MemberFilter(property);
        }

        private static bool HasRuleAccessibleAttribute(PropertyInfo property)
        {
            if (property.IsDefined(typeof(RuleAccessibleAttribute), true))
            {
                return true;
            }

            Type? declaringType = property.DeclaringType;
            return declaringType != null && declaringType.IsDefined(typeof(RuleAccessibleAttribute), true);
        }
    }
}
