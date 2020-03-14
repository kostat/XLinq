using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Streamx.Linq.SQL.Grammar;

namespace Streamx.Linq.SQL.EFCore.DSL {
    static class Expressions {
        public static ExpressionType getExpressionType(this Expression p) => p.NodeType;
        public static Type getResultType(this Expression p) => p.Type;
        public static ParameterExpression Parameter(Type type, int index) => Expression.Parameter(type, "P_" + index);
        public static ParameterExpression parameter(Type type, int index) => Expression.Parameter(type, "P_" + index);
        public static T As<T>(this Expression e) where T : class => (XExpression<T>) e;
        public static XExpression<T> AsXExpression<T>(this T f) where T : Delegate => (XExpression<T>) f;
        public static bool IsSynthetic(this MemberInfo typeInfo) => typeInfo.IsDefined(typeof(CompilerGeneratedAttribute));
        public static bool IsLocal(this MemberInfo typeInfo) => typeInfo.IsDefined(typeof(LocalAttribute));
        public static bool IsTuple(this MemberInfo typeInfo) => typeInfo.IsDefined(typeof(TupleAttribute));
        public static bool IsNotation(this MemberInfo t) {
            foreach (var a in t.GetCustomAttributes()) {
                if (a.GetType().IsDefined(typeof(NotationAttribute)))
                    return true;
            }

            return false;
        }
    }


    static class Arrays {
        public static void Fill<T>(this T[] array, T value) {
#if NETFRAMEWORK
            for (var i = 0; i < array.Length; i++) {
                array[i] = value;
            }
#else
            Array.Fill(array, value);
#endif
        }
    }
}
