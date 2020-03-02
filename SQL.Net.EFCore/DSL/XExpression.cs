using System.Linq.Expressions;

namespace Streamx.Linq.SQL.EFCore.DSL {
    sealed class XExpression<T> : Expression {
        public XExpression(T x) {
            X = x;
        }
        public static explicit operator XExpression<T>(T x) => new XExpression<T>(x);
        public static implicit operator T(XExpression<T> x) => x.X;

        private T X { get; }
    }
}