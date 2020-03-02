using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Streamx.Linq.ExTree {
    internal static class Expressions {
        private static readonly Expression ZERO = Expression.Constant(0);
        private static readonly Expression NULL = Expression.Constant(null);
        public static readonly Expression TRUE = Expression.Constant(true);

        public static bool IsInt31(this Expression e) =>
            e is ConstantExpression eConst && ((e.IsInt32() && (int) eConst.Value == 31) || (e.IsBool() && (bool)eConst.Value));

        public static bool IsSynthetic(this MemberInfo typeInfo) =>
            typeInfo.IsDefined(typeof(CompilerGeneratedAttribute));

        public static bool IsConstBoolLike(this Expression e, out bool value) {
            if (e is ConstantExpression eConst) {

                if (eConst.Value == null) {
                    value = false;
                    return true;
                }

                if (e.IsInt32()) {
                    var i = (int) eConst.Value;
                    value = i != 0;
                    return i == 0 || i == 1;
                }

                value = (bool) eConst.Value;
                return true;
            }

            return value = false;
        }

        public static bool IsBool(this Expression e) => e.Type == typeof(bool);
        private static bool IsInt32(this Expression e) => e.Type == typeof(int);

        private static BinaryExpression CreateNumeric(ExpressionType expressionType, Expression left, Expression right) {
            var isLNumeric = left.Type.IsNumeric();
            var isRNumeric = right.Type.IsNumeric();

            if (!isLNumeric || !isRNumeric) {
                if (isLNumeric)
                    right = TypeConverter.Convert(right, left.Type);
                else
                    left = TypeConverter.Convert(left, right.Type);
            }

            return Expression.MakeBinary(expressionType, left, right);
        }

        public static BinaryExpression Add(Expression first,
            Expression second) {
            return CreateNumeric(ExpressionType.Add, first, second);
        }

        public static BinaryExpression Divide(Expression first,
            Expression second) {
            return CreateNumeric(ExpressionType.Divide, first, second);
        }

        public static BinaryExpression Subtract(Expression first,
            Expression second) {
            return CreateNumeric(ExpressionType.Subtract, first, second);
        }

        public static BinaryExpression Multiply(Expression first,
            Expression second) {
            return CreateNumeric(ExpressionType.Multiply, first, second);
        }

        public static BinaryExpression Modulo(Expression first,
            Expression second) {
            return CreateNumeric(ExpressionType.Modulo, first, second);
        }

        private static Expression CreateBooleanExpression(ExpressionType expressionType,
            Expression first,
            Expression second) {
            Expression toReduce;
            Expression toLeave;

            if (first.NodeType == ExpressionType.Constant) {
                toReduce = first;
                toLeave = second;
            }
            else if (second.NodeType == ExpressionType.Constant) {
                toReduce = second;
                toLeave = first;
            }
            else {
                toReduce = null;
                toLeave = null;
            }

            if (toLeave != null && toLeave.IsBool()) {
                toReduce = TypeConverter.Convert(toReduce, typeof(bool));
                switch (expressionType) {
                    case ExpressionType.Equal:
                        return (Boolean) ((ConstantExpression) toReduce).Value ? toLeave : LogicalNot(toLeave);
                    case ExpressionType.NotEqual:
                        return (Boolean) ((ConstantExpression) toReduce).Value ? LogicalNot(toLeave) : toLeave;
                    case ExpressionType.AndAlso:
                        return (Boolean) ((ConstantExpression) toReduce).Value ? toLeave : toReduce;
                    case ExpressionType.OrElse:
                        return (Boolean) ((ConstantExpression) toReduce).Value ? toReduce : toLeave;
                }
            }

            return Expression.MakeBinary(expressionType, first, TypeConverter.Convert(second, first.Type));
        }

        public static Expression Equal(Expression first,
            Expression second) {
            return CreateBooleanExpression(ExpressionType.Equal, first, second);
        }

        public static Expression NotEqual(Expression first,
            Expression second) {
            return CreateBooleanExpression(ExpressionType.NotEqual, first, second);
        }

        public static Expression LogicalAnd(Expression first,
            Expression second) {
            return CreateBooleanExpression(ExpressionType.AndAlso, first, second);
        }

        public static Expression LogicalOr(Expression first,
            Expression second) {
            return CreateBooleanExpression(ExpressionType.OrElse, first, second);
        }

        public static Expression LogicalNot(Expression e) {
            if (!e.IsBool())
                throw new ArgumentException(e.Type.ToString());

            BinaryExpression be;

            ExpressionType type;
            switch (e.NodeType) {
                case ExpressionType.Conditional:
                    var x = (ConditionalExpression) e;
                    return Expression.Condition(x.Test, LogicalNot(x.IfTrue), LogicalNot(x.IfFalse));

                case ExpressionType.Constant:
                    var ce = (ConstantExpression) e;
                    return Expression.Constant(!(bool) ce.Value, ce.Type);

                case ExpressionType.Not:
                    var ue = (UnaryExpression) e;
                    return ue.Operand;

                case ExpressionType.AndAlso:
                    be = (BinaryExpression) e;
                    return LogicalOr(LogicalNot(be.Left), LogicalNot(be.Right));

                case ExpressionType.OrElse:
                    be = (BinaryExpression) e;
                    return LogicalAnd(LogicalNot(be.Left), LogicalNot(be.Right));

                case ExpressionType.Equal:
                    type = ExpressionType.NotEqual;
                    break;
                case ExpressionType.GreaterThan:
                    type = ExpressionType.LessThanOrEqual;
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    type = ExpressionType.LessThan;
                    break;
                case ExpressionType.LessThan:
                    type = ExpressionType.GreaterThanOrEqual;
                    break;
                case ExpressionType.LessThanOrEqual:
                    type = ExpressionType.GreaterThan;
                    break;
                case ExpressionType.NotEqual:
                    type = ExpressionType.Equal;
                    break;
                default:
                    return Expression.Not(e);
            }

            be = (BinaryExpression) e;
            return Expression.MakeBinary(type, be.Left, be.Right);
        }

        public static Expression IsTrue(Expression e) {
            if (e.IsBool())
                return e;

            return NotEqual(e, e.IsInt32() ? ZERO : NULL);
        }

        public static Expression IsFalse(Expression e) {
            if (e.IsBool())
                return LogicalNot(e);

            return Equal(e, e.IsInt32() ? ZERO : NULL);
        }

        public static Expression Condition(Expression test,
            Expression ifTrue,
            Expression ifFalse) {
            if (!test.IsBool())
                throw new ArgumentException("test is " + test.Type);
            
            bool value;
            // reduce conditional
            if (ifTrue.IsConstBoolLike(out value)) {
                if (ifFalse.IsConstBoolLike(out bool value1)) {
                    return value == value1 ? Expression.Constant(value) : value ? test : LogicalNot(test);
                }

                return value ? LogicalOr(test, ifFalse) : LogicalAnd(LogicalNot(test), ifFalse);
            }

            if (ifFalse.IsConstBoolLike(out value))
                return value ? LogicalOr(LogicalNot(test), ifTrue) : LogicalAnd(test, ifTrue);

            if (ifTrue.Type != ifFalse.Type) {
                if (ifTrue.Type == typeof(object))
                    ifTrue = Expression.Convert(ifTrue, ifFalse.Type);
                else
                    ifFalse = Expression.Convert(ifFalse, ifTrue.Type);
            }

            return Expression.Condition(test, ifTrue, ifFalse);
        }
    }
}