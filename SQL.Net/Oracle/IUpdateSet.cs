using System;
using Streamx.Linq.SQL.Grammar;

namespace Streamx.Linq.SQL.Oracle {
    public interface IUpdateSet {
        [Function(OmitParentheses = true)]
        IClause SET(Action sql);
    }
}
