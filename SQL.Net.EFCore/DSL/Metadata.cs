using System;
using System.Collections.Generic;
using System.Reflection;

namespace Streamx.Linq.SQL.EFCore.DSL {
    sealed class ClassMeta {
        public List<ID> IDs { get; } = new List<ID>();
    }

    sealed class ID {
        public ID(ISequence<char> path, ISequence<char> column, MemberInfo member) {
            Path = path;
            Column = column;
            Member = member;
        }

        public ISequence<char> Path { get; }
        public ISequence<char> Column { get; }
        public MemberInfo Member { get; }
    }

    sealed class Association {
        public IList<ISequence<char>> Left { get; }
        public IList<ISequence<char>> Right { get; }
        public int Cardinality => Left.Count;

        public Association(IList<ISequence<char>> that, IList<ISequence<char>> other, bool left) {
            if (that.Count != other.Count)
                throw new ArgumentException($"keys of different sizes: {that.Count}-{other.Count}");
            if (left) {
                Left = that;
                Right = other;
            }
            else {
                Left = other;
                Right = that;
            }
        }
    }
}