using Streamx.Linq.SQL.Grammar;

namespace Streamx.Linq.SQL {
    [NoOp]
    public interface IClause { }
    
    public interface IColumnsClause<in T> : IClause { }
}