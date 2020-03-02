using System;
using System.Linq.Expressions;
using System.Numerics;

namespace Streamx.Linq.ExTree {
    internal sealed class TypeConverter : ExpressionVisitor {
        private readonly Type _to;

        private TypeConverter(Type to) {
            _to = to;
        }

        public static Expression Convert(Expression e, Type to) {
            if (to.IsAssignableFrom(e))
                return e;

            return new TypeConverter(to).Visit(e);
        }

        private Object Convert(Type from, Object value) {
            
            if (_to.IsEnum) {
                return Enum.ToObject(_to, value);
            }
            
            if (from == typeof(int))
                return Convert((int) value);

            return DefaultConvert(value);
        }

        private Object Convert(int value) {
            if (_to == typeof(bool)) {
                if (value == 0)
                    return false;

                if (value == 1)
                    return true;
            }
            else if (_to == typeof(char)) {
                return (char) value;
            }
            else if (_to == typeof(byte)) {
                return (byte) value;
            }
            else if (_to == typeof(short)) {
                return (short) value;
            }

            return DefaultConvert(value);
        }

        private Expression DefaultConvert(Expression e) {
            if (_to.IsAssignableFrom(e))
                return e;

            return Expression.Convert(e, _to);
        }

        private Object DefaultConvert(Object value) {
            if (value != null && _to.IsInstanceOfType(value))
                throw new InvalidCastException(_to.ToString());

            return value;
        }

        protected override Expression VisitBinary(BinaryExpression node) {
            if (_to.IsAssignableFrom(node))
                return node;
            return Expression.MakeBinary(node.NodeType, Visit(node.Left), Visit(node.Right));
        }

        protected override Expression VisitConditional(ConditionalExpression node) {
            if (_to.IsAssignableFrom(node))
                return node;
            return Expression.Condition(node.Test, Visit(node.IfTrue), Visit(node.IfFalse));
        }

        protected override Expression VisitConstant(ConstantExpression node) {
            if (_to.IsAssignableFrom(node))
                return node;
            return Expression.Constant(Convert(node.Type, node.Value), _to);
        }

        protected override Expression VisitInvocation(InvocationExpression node) {
            return DefaultConvert(node);
        }

        protected override Expression VisitLambda<T>(Expression<T> node) {
            return DefaultConvert(node);
        }

        protected override Expression VisitMember(MemberExpression node) {
            return DefaultConvert(node);
        }

        protected override Expression VisitParameter(ParameterExpression node) {
            return DefaultConvert(node);
        }

        protected override Expression VisitUnary(UnaryExpression node) {
            return DefaultConvert(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node) {
            return DefaultConvert(node);
        }
    }

    internal static class TypeExtensions {
        public static bool IsAssignableFrom(this Type to, Expression from) {
            return to.IsAssignableFrom(from.Type);
        }

        private static bool IsIntegral(this Type t) {
            if (t.IsPrimitive) {
                var typeCode = Type.GetTypeCode(t);

                switch (typeCode) {
                    case TypeCode.Byte:
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                        return true;
                    default:
                        return false;
                }
            }

            return t == typeof(BigInteger);
        }

        public static bool IsNumeric(this Type t) {
            if (t.IsIntegral())
                return true;

            return (t.IsPrimitive) &&
                   (t == typeof(float) || t == typeof(double));
        }
    }
}