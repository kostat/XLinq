using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Streamx.Linq.ExTree {
    partial class MethodVisitor {
        public struct LocalVariable {
            
            public const string VARIABLE_PREFIX = "V_";

            public LocalVariableInfo Info { get; set; }
            private ParameterExpression Variable { get; set; }
            private Expression Expression { get; set; }
            private BinaryExpression Assignment { get; set; }

            public void Assign(Expression value, ExpressionStack stack) {
                Variable = value is ConstantExpression || value is ParameterExpression
                    ? null
                    :
                    // ReSharper disable once AssignNullToNotNullAttribute
                    Expression.Variable(Info.LocalType, VARIABLE_PREFIX + Info.LocalIndex);

                Expression = value;

                if (Variable != null) {
                    Assignment = Expression.Assign(Variable, Expression);
                    stack.TrackOrder(Assignment);
                }
            }

            public Expression Get(List<Expression> statements, List<ParameterExpression> variables) {
                if (Variable == null)
                    return Expression;

                if (variables.IndexOf(Variable) < 0) {
                    variables.Add(Variable);
                    statements.Add(Assignment);
                }

                return Variable;
            }
        }
    }
}