using System;

namespace Streamx.Linq.SQL.EFCore.DSL {
    static class Functions {
        public static Func<TV, TR> compose<T, TV, TR>(this Func<T, TR> after, Func<TV, T> before) => (TV v) => after(before(v));
        public static Func<T, TV> andThen<T, TV, TR>(this Func<T, TR> before, Func<TR, TV> after) => (T t) => after(before(t));
        public static Func<TV> andThen<TV, TR>(this Func<TR> before, Func<TR, TV> after) => () => after(before());
    }
}