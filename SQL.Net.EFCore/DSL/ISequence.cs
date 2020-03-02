using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Streamx.Linq.SQL.EFCore.DSL {
    interface ISequence<out T> : IEnumerable<T> {
        bool IsEmpty { get; }
        int Length { get; }
        T this[int index] { get; }
        ISequence<T> SubSequence(int start, int end);
    }

    sealed class StringSequence : ISequence<char>, IEquatable<StringSequence> {
        private readonly string _s;

        private StringSequence(string s) {
            _s = s;
        }

        public static ISequence<char> From(string s) => new StringSequence(s);

        public IEnumerator<char> GetEnumerator() => _s.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool IsEmpty => Length == 0;
        public int Length => _s.Length;

        public char this[int index] => _s[index];

        public override string ToString() => _s;

        public ISequence<char> SubSequence(int start, int end) => StringSequence.From(_s.Substring(start, end - start));

        public bool Equals(StringSequence other) {
            if (ReferenceEquals(this, other)) return true;
            if (ReferenceEquals(null, other)) return false;
            return ReferenceEquals(_s, other._s);
        }

        public override bool Equals(object obj) {
            return obj is StringSequence other && Equals(other);
        }

        public override int GetHashCode() {
            return (_s != null ? _s.GetHashCode() : 0);
        }
    }

    sealed class StringBuilderSequence : ISequence<char>, IEquatable<StringBuilderSequence> {
        private readonly StringBuilder _s;

        private StringBuilderSequence(StringBuilder s) {
            _s = s;
        }

        public static ISequence<char> From(StringBuilder s) => new StringBuilderSequence(s);

        public IEnumerator<char> GetEnumerator() {
            for (var i = 0; i < Length; i++)
                yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool IsEmpty => Length == 0;
        public int Length => _s.Length;

        public char this[int index] => _s[index];

        public override string ToString() => _s.ToString();

        public ISequence<char> SubSequence(int start, int end) => _s.ToString(start, end - start).AsSequence();

        public bool Equals(StringBuilderSequence other) {
            if (ReferenceEquals(this, other)) return true;
            if (ReferenceEquals(null, other)) return false;
            return ReferenceEquals(_s, other._s);
        }

        public override bool Equals(object obj) {
            return obj is StringBuilderSequence other && Equals(other);
        }

        public override int GetHashCode() {
            return (_s != null ? _s.GetHashCode() : 0);
        }
    }

    abstract class DelegatedSequence : ISequence<char>, IEquatable<DelegatedSequence> {

        public abstract ISequence<char> Wrapped { get; }
        
        public IEnumerator<char> GetEnumerator() => Wrapped.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public virtual bool IsEmpty => Wrapped.IsEmpty;

        public int Length => Wrapped.Length;

        public char this[int index] => Wrapped[index];

        public ISequence<char> SubSequence(int start, int end) => Wrapped.SubSequence(start, end);

        public override string ToString() => Wrapped.ToString();
        public bool Equals(DelegatedSequence other) {
            if (ReferenceEquals(this, other)) return true;
            if (ReferenceEquals(null, other)) return false;
            return Wrapped.Equals(other.Wrapped);
        }

        public override bool Equals(object obj) => obj is DelegatedSequence other && Equals(other);

        public override int GetHashCode() => Wrapped.GetHashCode();
    }
}