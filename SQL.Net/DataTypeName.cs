using System;
using Streamx.Linq.SQL.Grammar;

namespace Streamx.Linq.SQL {
    public abstract class DataTypeName : EnumWithBlanks {
        [Local]
        public DataType<T> Create<T>() where T : IComparable<T> {
            var typeName = ToString();
            return DataType<T>.Create(typeName);
        }

        [Local]
        public DataType<T> Create<T>(int length) where T : IComparable<T> {
            var created = Create<T>();
            return created.Length(length);
        }

        [Local]
        public DataType<T> Create<T>(int precision, int scale) where T : IComparable<T> {
            var created = Create<T>();
            return created.Numeric(precision, scale);
        }
    }
}
