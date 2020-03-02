using System.Collections;
using System.Collections.Generic;

namespace Streamx.Linq.SQL.EFCore.DSL {
    class SubQueryManager : ScopedList<SubQueryManager.SubQuery> {
        public class SubQuery : ISequence<char> {
            private ISequence<char> value;

            public ISequence<char> name { get; }
            public ISequence<char> expression { get; }
            public bool requiresParentheses { get; }

            public SubQuery(ISequence<char> name, ISequence<char> value, bool requiresParentheses) {
                this.name = name;
                this.expression = value;
                this.value = value;
                this.requiresParentheses = requiresParentheses;
            }

            public bool isName() {
                return value == name;
            }

            public bool flip(bool toName) {
                ISequence<char> prev = value;
                value = toName ? name : expression;
                return prev != value;
            }


            public IEnumerator<char> GetEnumerator() => value.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public virtual bool IsEmpty => value.IsEmpty;

            public int Length => value.Length;

            public char this[int index] => value[index];

            public ISequence<char> SubSequence(int start, int end) => value.SubSequence(start, end);

            public override string ToString() => value.ToString();
        }

        private IList<SubQuery> sealedNames = Collections.emptyList<SubQuery>();

        public SubQueryManager(IList<SubQuery> upper) : base(upper) { }

        public ISequence<char> put(ISequence<char> name,
            ISequence<char> value) {
            return put(name, value, true);
        }

        public ISequence<char> put(ISequence<char> name,
            ISequence<char> value,
            bool requiresParentheses) {
            SubQuery subQuery = new SubQuery(name, value, requiresParentheses);
            Add(subQuery);

            return subQuery;
        }

        public ISequence<char> sealName(ISequence<char> seq) {
            if (!isSubQuery(seq))
                return null;
            SubQuery subQuery = (SubQuery) seq;
            if (!subQuery.flip(true))
                return null;

            if (sealedNames.IsEmpty())
                sealedNames = new List<SubQuery>();

            sealedNames.Add(subQuery);

            return subQuery.expression;
        }

        public static bool isSubQuery(ISequence<char> seq) {
            return seq is SubQuery;
        }

        public static bool isSubQueryName(ISequence<char> seq) {
            return isSubQuery(seq) && ((SubQuery) seq).isName();
        }

        public static bool isSubQueryExpression(ISequence<char> seq) {
            return isSubQuery(seq) && !((SubQuery) seq).isName();
        }

        public static ISequence<char> getName(ISequence<char> seq) {
            SubQuery subQuery = (SubQuery) seq;
            return subQuery.isName() ? subQuery : subQuery.name;
        }

        public static bool isRequiresParentheses(ISequence<char> seq) {
            SubQuery subQuery = (SubQuery) seq;
            return subQuery.requiresParentheses;
        }

        public void close() {
            foreach (SubQuery subQuery in sealedNames)
                subQuery.flip(false);
        }
    }
}