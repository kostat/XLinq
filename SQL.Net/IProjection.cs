using System;
using System.Runtime.CompilerServices;
using Streamx.Linq.SQL.Grammar;

namespace Streamx.Linq.SQL {

    public interface IProjection<T> where T : class {
        [Function(""), ViewDeclarationAttribute.From(Aliased = true)]
        T EntityAliased(T source, params IComparable[] overrides);
    }
    
    public interface IProjection<T, TTuple> : IProjection<T>
        where T : class
        where TTuple : struct, ITuple {
        
        [Function(""), ViewDeclarationAttribute.From]
        IColumnsClause<T, TTuple> ColumnNames();

        [Function(""), ViewDeclarationAttribute.Row]
        TTuple Row(TTuple tuple);
        
        [Function(""), ViewDeclarationAttribute.Row(Aliased = true)]
        T Entity(TTuple tuple);

        [Function(""), ViewDeclarationAttribute.From(Aliased = true)]
        T EntityAliased(TTuple tuple);
        
        [Function(""), ViewDeclarationAttribute.From]
        TTuple RowFrom(T source, params IComparable[] overrides);
        
        [Function(""), ViewDeclarationAttribute.From]
        TTuple RowFrom(TTuple tuple, params IComparable[] overrides);
    }
}
