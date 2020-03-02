using System;
using System.Collections.Generic;
using Streamx.Linq.SQL.Grammar;

namespace Streamx.Linq.SQL {
    public interface ITupleSelect<T> {
        [NoOp]
        IQueryResult<T> AsSingle(); //row value
        [NoOp]
        IQueryResult<ICollection<T>> AsCollection();
    }

    public interface ISelectClause : IClause { }

    [NoOp]
    public interface IEntitySelectClause<out T> : ISelectClause, IQueryResult<T> where T : class {
        [CommonTableExpression(CommonTableExpressionType.Self)]
        T Self();
    }
    
    [NoOp]
    public interface ITupleSelectClause<T> : ISelectClause, ITupleSelect<T> { }
}