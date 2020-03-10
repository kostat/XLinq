using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Streamx.Linq.SQL.EFCore.DSL {
    abstract class IdentifierPath : ISequence<char> {
        public const char DOT = '.';
        public abstract IEnumerator<char> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public virtual bool IsEmpty => false;
        public abstract int Length { get; }
        public abstract char this[int index] { get; }
        public abstract ISequence<char> SubSequence(int start, int end);

        public virtual ISequence<char> resolveInstance(ISequence<char> inst,
            IDictionary<string, ISequence<char>> secondaryResolver) {
            return resolveInstance(inst, false, secondaryResolver);
        }

        public abstract ISequence<char> resolveInstance(ISequence<char> inst,
            bool withoutInstance,
            IDictionary<string, ISequence<char>> secondaryResolver);

        public virtual ISequence<char> resolve(IdentifierPath path) {
            return resolve(path, false);
        }

        public abstract ISequence<char> resolve(IdentifierPath path,
            bool withoutInstance);

        public abstract ISequence<char> Current { get; }

        public abstract Type DeclaringClass { get; }

        public abstract string FieldName { get; }

        public static ISequence<char> current(ISequence<char> seq) {
            return seq is IdentifierPath path ? path.Current : seq;
        }

        public static bool isResolved(ISequence<char> seq) {
            return seq is Resolved;
        }

        static ISequence<char> resolveInstance(ISequence<char> inst,
            String table,
            Type declaringClass,
            IDictionary<String, ISequence<char>> secondaryResolver) {
            if (secondaryResolver != null) {
                if (String.IsNullOrEmpty(table))
                    table = declaringClass.Name;
                ISequence<char> inst1 = secondaryResolver.get(table);
                if (inst1 == null)
                    inst1 = secondaryResolver.get("");

                if (inst1 != null)
                    inst = inst1;
            }

            return inst;
        }


        public class Resolved : IdentifierPath {
            private readonly ISequence<char> resolution;

            public override Type DeclaringClass { get; }


            public override string FieldName { get; }

            private readonly String table;

            public Resolved(ISequence<char> resolution, Type declaringClass, string fieldName, string table) {
                this.resolution = resolution;
                DeclaringClass = declaringClass;
                FieldName = fieldName;
                this.table = table;
            }

            public override IEnumerator<char> GetEnumerator() {
                return resolution.GetEnumerator();
            }

            public override int Length => resolution.Length;

            public override char this[int index] => resolution[index];

            public override ISequence<char> SubSequence(int start, int end) => resolution.SubSequence(start, end);


            public override ISequence<char> resolveInstance(ISequence<char> inst,
                bool withoutInstance,
                IDictionary<String, ISequence<char>> secondaryResolver) {
                if (inst is DSLInterpreter.ParameterRef @ref)
                    throw TranslationError.CANNOT_DEREFERENCE_PARAMETERS.getError(@ref.Value,
                        resolution);
                if (inst.isNullOrEmpty())
                    return this;
                if (inst is IdentifierPath path)
                    return path.resolve(this, withoutInstance);

                inst = IdentifierPath.resolveInstance(inst, table, DeclaringClass, secondaryResolver);

                StringBuilder seq = withoutInstance ? new StringBuilder() : new StringBuilder().Append(inst).Append(DOT);
                return new Resolved(seq.Append(resolution).AsSequence(), DeclaringClass, FieldName, table);
            }


            public override ISequence<char> resolve(IdentifierPath path,
                bool withoutInstance) {
                return this;
            }


            public override ISequence<char> Current {
                get { return resolution; }
            }


            public override String ToString() {
                return resolution.ToString();
            }
        }

        public abstract class AssociativeIdentifierPath : IdentifierPath {
            public override string FieldName { get; }
            public String Table { get; }
            public ISequence<char> Instance { get; set; }

            protected AssociativeIdentifierPath(string fieldName, string table) {
                FieldName = fieldName;
                this.Table = table;
            }

            protected SystemException error() {
                return new InvalidOperationException(
                    $"'{FieldName}' has multi-column mapping. You must call the appropriate property to resolve it");
            }

            public override Type DeclaringClass => throw new NotImplementedException();

            public override int Length => throw error();

            public override char this[int index] => throw error();

            public override ISequence<char> SubSequence(int start, int end) => throw error();

            public override string ToString() => throw error();

            public override ISequence<char> Current => Instance;

            public override ISequence<char> resolveInstance(ISequence<char> inst,
                bool withoutInstance,
                IDictionary<String, ISequence<char>> secondaryResolver) {
                if (this.Instance != null)
                    throw new InvalidOperationException(
                        $"Already initialized with '{this.Instance}' instance. Passing a new '{inst}' is illegal");
                inst = IdentifierPath.resolveInstance(inst, Table, typeof(void), secondaryResolver);
                this.Instance = inst;
                return this;
            }
        }

        public sealed class MultiColumnIdentifierPath : AssociativeIdentifierPath {
            private readonly Func<Type, Association> associationSupplier;

            public MultiColumnIdentifierPath(String originalField,
                Func<Type, Association> associationSupplier, String table) : base(originalField, table) {
                this.associationSupplier = associationSupplier;
            }

            public override IEnumerator<char> GetEnumerator() => throw error();

            public override ISequence<char> resolve(IdentifierPath path,
                bool withoutInstance) {
                if (!(path is Resolved))
                    throw new ArgumentException(path.GetType() + ":" + path.Current);
                ISequence<char> key = path.Current;
                Association association = associationSupplier(path.DeclaringClass);
                var referenced = association.Right;
                for (int i = 0; i < referenced.Count; i++) {
                    ISequence<char> seq = referenced.get(i);
                    if (seq.equals(key)) {
                        StringBuilder inst = withoutInstance
                            ? new StringBuilder()
                            : new StringBuilder().Append(Instance).Append(DOT);
                        return new Resolved(inst.Append(association.Left[i]).AsSequence(), path.DeclaringClass,
                            path.FieldName, Table);
                    }
                }

                throw new ArgumentException("Column '" + key + "' not found in PK: " + referenced);
            }
        }

        sealed class ColumnOverridingIdentifierPath : AssociativeIdentifierPath {
            private readonly IDictionary<String, String> overrides;

            public ColumnOverridingIdentifierPath(IDictionary<String, String> overrides, String table) :
                base(null, table) {
                this.overrides = overrides;
            }

            public override IEnumerator<char> GetEnumerator() => Current.GetEnumerator();

            public override int Length => Current.Length;

            public override char this[int index] => Current[index];

            public override ISequence<char> SubSequence(int start, int end) => Current.SubSequence(start, end);

            public override string ToString() => Current.ToString();


            public override ISequence<char> resolve(IdentifierPath path,
                bool withoutInstance) {
                if (!(path is Resolved)) // TODO: can be MultiColumnIdentifierPath
                    throw new ArgumentException(path.GetType() + ":" + path.Current);
                String key = path.FieldName;
                String @override = overrides[key];
                ISequence<char> column = @override != null ? @override.AsSequence() : path.Current;

                StringBuilder inst = withoutInstance ? new StringBuilder() : new StringBuilder().Append(Instance).Append(DOT);
                return new Resolved(inst.Append(column).AsSequence(), path.DeclaringClass, key, Table);
            }
        }
    }
}