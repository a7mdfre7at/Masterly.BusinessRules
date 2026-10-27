namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// The entry points compiled expression trees call at run time. They reuse the interpreter's binding and coercion
    /// code so the typed path and the untyped path cannot disagree.
    /// </summary>
    internal static class RuleExpressionRuntime
    {
        public static object? BindIdentifier(BusinessRuleContext context, string name)
        {
            if (context.ContainsKey(name))
            {
                return context.Get<object>(name);
            }

            throw new RuleExpressionBindingException(name, context.Keys);
        }

        public static object? BindParameter(RuleExpressionParameterSource source, BusinessRuleContext context, string name, string nodeText)
        {
            if (source.TryResolve(name, out object? value))
            {
                return value;
            }

            if (context.ContainsKey(name))
            {
                return context.Get<object>(name);
            }

            throw new RuleExpressionBindingException(nodeText, context.Keys);
        }

        public static RuleExpressionParameterSource CreateParameterSource(BusinessRuleContext context, IRuleParameterProvider? parameters)
            => RuleExpressionParameterSource.ForSync(context, null, parameters);

        public static object? GetMember(object? target, string member, RuleExpressionOptions options, string nodeText)
            => RuleExpressionMemberAccessor.GetValue(target, member, options, nodeText);

        public static object AreEqual(object? left, object? right, bool ignoreCase) => RuleExpressionValues.AreEqual(left, right, ignoreCase);

        public static object AreNotEqual(object? left, object? right) => !RuleExpressionValues.AreEqual(left, right, false);

        public static object Compare(BinaryOperator op, object? left, object? right, string nodeText) => RuleExpressionValues.Compare(op, left, right, nodeText);

        public static object? Arithmetic(BinaryOperator op, object? left, object? right, string nodeText) => RuleExpressionValues.Arithmetic(op, left, right, nodeText);

        public static object? Negate(object? value, string nodeText) => RuleExpressionValues.Negate(value, nodeText);

        public static bool ToBoolean(object? value, string nodeText) => RuleExpressionValues.ToBoolean(value, nodeText);

        public static object Not(object? value, string nodeText) => !RuleExpressionValues.ToBoolean(value, nodeText);

        public static bool IsItem(object? value, object? item) => RuleExpressionValues.AreEqual(value, item, false);

        public static object IsInCollection(object? value, object? collection, string nodeText) => RuleExpressionValues.IsInCollection(value, collection, nodeText);

        public static object InvokeFunction(ExpressionFunction function, object? target, object? argument, bool ignoreCase, string nodeText)
            => RuleExpressionValues.InvokeFunction(function, target, argument, ignoreCase, nodeText);
    }
}
