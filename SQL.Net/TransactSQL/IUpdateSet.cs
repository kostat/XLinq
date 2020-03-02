using System;
using Streamx.Linq.SQL.Grammar;

namespace Streamx.Linq.SQL.TransactSQL {
    public interface IUpdateSet {
        [Function(OmitParentheses = true)]
        IClause SET(Action action);
        
        [Function(OmitParentheses = true)]
        IClause SET(IClause action);
    }
}
