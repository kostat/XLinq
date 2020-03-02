namespace Streamx.Linq.SQL.EFCore.DSL {
    static class UnaryOperator {
        public static ISequence<char> IsNull(this ISequence<char> operand) => $"{operand} IS NULL".AsSequence();
        public static ISequence<char> IsNotNull(this ISequence<char> operand) => $"{operand} IS NOT NULL".AsSequence();
        public static ISequence<char> LogicalNot(this ISequence<char> operand) => $"NOT({operand})".AsSequence();
        public static ISequence<char> Negate(this ISequence<char> operand) => $"-{operand}".AsSequence();
        public static ISequence<char> BitwiseNot(this ISequence<char> operand) => $"~{operand}".AsSequence();
    }
}