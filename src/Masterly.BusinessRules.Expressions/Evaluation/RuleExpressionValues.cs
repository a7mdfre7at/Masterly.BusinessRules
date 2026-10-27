using System;
using System.Collections;
using System.Globalization;

namespace Masterly.BusinessRules.Expressions
{
    /// <summary>
    /// The coercion rules of the expression language, shared by the interpreter and the typed compiler so both paths
    /// agree by construction. Numbers promote to <see cref="decimal"/> (to <see cref="double"/> when either side is a
    /// float or double); enums compare to strings by name ignoring case and to numbers by their underlying value;
    /// strings compare ordinally (<c>~=</c> ignores case); null-safe paths yield null and every comparison with null is
    /// false except <c>==</c>/<c>!=</c>; mismatched types are never equal and never ordered.
    /// </summary>
    internal static class RuleExpressionValues
    {
        public static bool IsNumeric(object? value)
        {
            switch (value)
            {
                case sbyte _:
                case byte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                case ulong _:
                case float _:
                case double _:
                case decimal _:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsFloating(object? value) => value is float || value is double;

        public static bool AreEqual(object? left, object? right, bool ignoreCase)
        {
            if (left == null || right == null)
            {
                return left == null && right == null;
            }

            if (IsNumeric(left) && IsNumeric(right))
            {
                return CompareNumbers(left, right) == 0;
            }

            if (left is Enum || right is Enum)
            {
                return EnumEquals(left, right);
            }

            if (left is string leftText && right is string rightText)
            {
                return string.Equals(leftText, rightText, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            }

            return left.GetType() == right.GetType() && left.Equals(right);
        }

        public static bool Compare(BinaryOperator op, object? left, object? right, string nodeText)
        {
            if (left == null || right == null)
            {
                return false;
            }

            int comparison = Order(left, right, nodeText);
            switch (op)
            {
                case BinaryOperator.Less:
                    return comparison < 0;
                case BinaryOperator.LessOrEqual:
                    return comparison <= 0;
                case BinaryOperator.Greater:
                    return comparison > 0;
                default:
                    return comparison >= 0;
            }
        }

        public static object? Arithmetic(BinaryOperator op, object? left, object? right, string nodeText)
        {
            if (left == null || right == null)
            {
                return null;
            }

            if (left is string leftText && right is string rightText)
            {
                if (op == BinaryOperator.Add)
                {
                    return leftText + rightText;
                }

                throw new RuleExpressionEvaluationException(nodeText, $"Operator '{RuleExpressionTextRenderer.Symbol(op)}' cannot be applied to two strings");
            }

            if (!IsNumeric(left) || !IsNumeric(right))
            {
                throw new RuleExpressionEvaluationException(nodeText, $"Operator '{RuleExpressionTextRenderer.Symbol(op)}' requires numeric operands but got {TypeName(left)} and {TypeName(right)}");
            }

            try
            {
                if (IsFloating(left) || IsFloating(right))
                {
                    return DoubleArithmetic(op, Convert.ToDouble(left, CultureInfo.InvariantCulture), Convert.ToDouble(right, CultureInfo.InvariantCulture));
                }

                return DecimalArithmetic(op, Convert.ToDecimal(left, CultureInfo.InvariantCulture), Convert.ToDecimal(right, CultureInfo.InvariantCulture));
            }
            catch (DivideByZeroException exception)
            {
                throw new RuleExpressionEvaluationException(nodeText, "Division by zero", exception);
            }
            catch (OverflowException exception)
            {
                throw new RuleExpressionEvaluationException(nodeText, "Arithmetic overflow", exception);
            }
        }

        public static object? Negate(object? value, string nodeText)
        {
            if (value == null)
            {
                return null;
            }

            if (IsFloating(value))
            {
                return -Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }

            if (IsNumeric(value))
            {
                return -Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }

            throw new RuleExpressionEvaluationException(nodeText, $"Operator '-' requires a numeric operand but got {TypeName(value)}");
        }

        public static bool ToBoolean(object? value, string nodeText)
        {
            if (value is bool flag)
            {
                return flag;
            }

            throw new RuleExpressionEvaluationException(nodeText, $"Expected a boolean but got {TypeName(value)}");
        }

        public static bool IsInCollection(object? value, object? collection, string nodeText)
        {
            if (collection == null)
            {
                return false;
            }

            if (collection is string || !(collection is IEnumerable items))
            {
                throw new RuleExpressionEvaluationException(nodeText, $"Operator 'in' requires a list or collection on its right-hand side but got {TypeName(collection)}");
            }

            foreach (object? item in items)
            {
                if (AreEqual(value, item, false))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsInItems(object? value, object?[] items)
        {
            foreach (object? item in items)
            {
                if (AreEqual(value, item, false))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool InvokeFunction(ExpressionFunction function, object? target, object? argument, bool ignoreCase, string nodeText)
        {
            if (target == null || argument == null)
            {
                return false;
            }

            StringComparison comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (target is string text)
            {
                if (!(argument is string fragment))
                {
                    throw new RuleExpressionEvaluationException(nodeText, $"{RuleExpressionTextRenderer.FunctionName(function, ignoreCase)} requires a string argument but got {TypeName(argument)}");
                }

                switch (function)
                {
                    case ExpressionFunction.StartsWith:
                        return text.StartsWith(fragment, comparison);
                    case ExpressionFunction.EndsWith:
                        return text.EndsWith(fragment, comparison);
                    default:
                        return text.IndexOf(fragment, comparison) >= 0;
                }
            }

            if (function == ExpressionFunction.Contains && target is IEnumerable items)
            {
                foreach (object? item in items)
                {
                    if (AreEqual(item, argument, ignoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            throw new RuleExpressionEvaluationException(nodeText, $"{RuleExpressionTextRenderer.FunctionName(function, ignoreCase)} cannot be applied to {TypeName(target)}");
        }

        public static string TypeName(object? value) => value == null ? "null" : value.GetType().Name;

        private static int Order(object left, object right, string nodeText)
        {
            if (IsNumeric(left) && IsNumeric(right))
            {
                return CompareNumbers(left, right);
            }

            if (left is Enum || right is Enum)
            {
                return OrderEnums(left, right, nodeText);
            }

            if (left is string leftText && right is string rightText)
            {
                return Math.Sign(string.CompareOrdinal(leftText, rightText));
            }

            if (left.GetType() == right.GetType() && left is IComparable comparable)
            {
                return Math.Sign(comparable.CompareTo(right));
            }

            throw new RuleExpressionEvaluationException(nodeText, $"Cannot order {TypeName(left)} against {TypeName(right)}");
        }

        private static int CompareNumbers(object left, object right)
        {
            if (IsFloating(left) || IsFloating(right))
            {
                return Convert.ToDouble(left, CultureInfo.InvariantCulture).CompareTo(Convert.ToDouble(right, CultureInfo.InvariantCulture));
            }

            return Convert.ToDecimal(left, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(right, CultureInfo.InvariantCulture));
        }

        private static bool EnumEquals(object left, object right)
        {
            if (left is Enum && right is Enum)
            {
                return left.GetType() == right.GetType() && left.Equals(right);
            }

            Enum enumValue = left is Enum leftEnum ? leftEnum : (Enum)right;
            object other = left is Enum ? right : left;

            if (other is string name)
            {
                return string.Equals(enumValue.ToString(), name, StringComparison.OrdinalIgnoreCase);
            }

            if (IsNumeric(other))
            {
                return CompareNumbers(EnumToNumber(enumValue), other) == 0;
            }

            return false;
        }

        private static int OrderEnums(object left, object right, string nodeText)
        {
            if (left is Enum leftEnum && right is Enum rightEnum)
            {
                if (left.GetType() != right.GetType())
                {
                    throw new RuleExpressionEvaluationException(nodeText, $"Cannot order {TypeName(left)} against {TypeName(right)}");
                }

                return CompareNumbers(EnumToNumber(leftEnum), EnumToNumber(rightEnum));
            }

            object leftNumber = left is Enum le ? EnumToNumber(le) : left;
            object rightNumber = right is Enum re ? EnumToNumber(re) : right;
            if (IsNumeric(leftNumber) && IsNumeric(rightNumber))
            {
                return CompareNumbers(leftNumber, rightNumber);
            }

            throw new RuleExpressionEvaluationException(nodeText, $"Cannot order {TypeName(left)} against {TypeName(right)}");
        }

        private static object EnumToNumber(Enum value) => Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()), CultureInfo.InvariantCulture);

        private static object DecimalArithmetic(BinaryOperator op, decimal left, decimal right)
        {
            switch (op)
            {
                case BinaryOperator.Add:
                    return left + right;
                case BinaryOperator.Subtract:
                    return left - right;
                case BinaryOperator.Multiply:
                    return left * right;
                case BinaryOperator.Divide:
                    return left / right;
                default:
                    return left % right;
            }
        }

        private static object DoubleArithmetic(BinaryOperator op, double left, double right)
        {
            switch (op)
            {
                case BinaryOperator.Add:
                    return left + right;
                case BinaryOperator.Subtract:
                    return left - right;
                case BinaryOperator.Multiply:
                    return left * right;
                case BinaryOperator.Divide:
                    return left / right;
                default:
                    return left % right;
            }
        }
    }
}
