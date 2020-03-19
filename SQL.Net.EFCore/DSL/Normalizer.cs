using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Streamx.Linq.ExTree;
using Streamx.Linq.SQL.Grammar;

namespace Streamx.Linq.SQL.EFCore.DSL {
    sealed class Normalizer : ExpressionVisitor {
        protected override Expression VisitMethodCall(MethodCallExpression node) {

            if (node.Method.IsLocal()) {
                var result = node.Method.Invoke(node.Object != null ? Expression.Lambda(node.Object).Compile().DynamicInvoke() : null,
                    EvaluateArgumentsForLocalFunction(node.Arguments));
                // TODO: re-parse
                return Expression.Constant(result, node.Type);
            }

            if (XLinq.GetSubstitition(node.Method, out var subst)) {
                var replaced = new Replacer(node.Object != null ? Visit(node.Object) : null, Visit(node.Arguments), subst.Parameters).Visit(subst.Expression);
                return Replacer.Convert(Visit(replaced), node.Type);
            }

            if (!node.Method.IsAbstract && !node.Method.IsSpecialName) {
                if (node.Method.IsNotation())
                    return base.VisitMethodCall(node);

                var e = ExpressionTree.Parse(node.Object, node.Method, node.Arguments);
                XLinq.PrintExpression(e);
                return Visit(Expression.Invoke(e, node.Arguments));
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitMember(MemberExpression node) {

            if (node.Expression == null && node.Member is FieldInfo fi) {
                var value = fi.GetValue(null);
                return Expression.Constant(value);
            }

            return base.VisitMember(node);
        }

        static object[] EvaluateArgumentsForLocalFunction(IEnumerable<Expression> args) {
            return args.Select(_ => Expression.Lambda(_).Compile().DynamicInvoke()).ToArray();
        }

        sealed class Replacer : ExpressionVisitor {
            public Replacer(Expression instance, IList<Expression> arguments, IList<ParameterExpression> parameters) {
                Instance = instance;
                Arguments = arguments;
                Parameters = parameters;
            }

            private Expression Instance { get; }
            private IList<Expression> Arguments { get; }
            private IList<ParameterExpression> Parameters { get; }

            protected override Expression VisitParameter(ParameterExpression node) {
                var index = Parameters.IndexOf(node);

                if (Instance == null)
                    return Convert(Arguments[index], node.Type);

                return Convert(index == 0 ? Instance : Arguments[--index], node.Type);
            }

            public static Expression Convert(Expression expression, Type type) =>
                type.IsAssignableFrom(expression.Type) ? expression : Expression.Convert(Expression.Convert(expression, typeof(object)), type);
        }
    }
}
