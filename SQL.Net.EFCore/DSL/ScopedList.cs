using System.Collections.Generic;

namespace Streamx.Linq.SQL.EFCore.DSL {
    class ScopedList<T> : List<T>, IList<T> {
        protected ScopedList(IList<T> upper) {
            Upper = upper;
        }

        protected IList<T> Upper { get; }

        public new int Count => base.Count + Upper.Count;

        public new bool Contains(T item) => base.Contains(item) || Upper.Contains(item);
    }
}