using System;

namespace Streamx.Linq.SQL.PostgreSQL {
    public static class DataTypes {
        public static readonly DataType<DateTimeOffset> TIMESTAMP = DataTypeNames.TIMESTAMP.Create<DateTimeOffset>();
        public static readonly DataType<DateTime> DATE = DataTypeNames.DATE.Create<DateTime>();
        public static readonly DataType<DateTime> TIME = DataTypeNames.TIME.Create<DateTime>();
        public static readonly DataType<DateTimeOffset> INTERVAL = DataTypeNames.INTERVAL.Create<DateTimeOffset>();

        /**
     * 2 bytes
     */
        public static readonly DataType<short> SMALLINT = DataTypeNames.SMALLINT.Create<short>();

        /**
     * 4 bytes
     */
        public static readonly DataType<int> INT = DataTypeNames.INTEGER.Create<int>();

        /**
     * 8 bytes
     */
        public static readonly DataType<long> BIGINT = DataTypeNames.BIGINT.Create<long>();

        // public static readonly DataType<BigDecimal> DECIMAL = DataTypeNames.NUMERIC.Create();
        // public static readonly DataType<BigDecimal> NUMERIC = DataTypeNames.NUMERIC.Create();

        public static readonly DataType<float> REAL = DataTypeNames.REAL.Create<float>();
        public static readonly DataType<double> DOUBLE = DataTypeNames.FLOAT8.Create<double>();

        // Serial types effectively Create an appropriate integer column type

        //public static readonly DataType<BigDecimal> MONEY = DataTypeNames.MONEY.Create<>();

        public static readonly DataType<bool> BOOLEAN = DataTypeNames.BOOLEAN.Create<bool>();
    }
}
