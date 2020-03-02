using System;

namespace Streamx.Linq.SQL.Grammar {
    // enum can be used as part of an expression
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Enum)]
    public sealed class LiteralAttribute : Attribute { }
}
