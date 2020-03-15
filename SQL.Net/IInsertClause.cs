using System;
using Streamx.Linq.SQL.Grammar;

namespace Streamx.Linq.SQL {
    public interface IInsertClause {
        [Function(OmitParentheses = true, ParameterContext = ParameterContext.FromWithoutAlias,
            ParameterContextCapabilities = new[] {nameof(Capability.ALIAS_INSERT)})]
        IClause INTO(Object tableReference);
        
        [Function(OmitParentheses = true, OmitArgumentsDelimiter = true, ParameterContext = ParameterContext.FromWithoutAlias,
            ParameterContextCapabilities = new[] {nameof(Capability.ALIAS_INSERT)})]
        IClause INTO<TE, T>(TE tableReference, IColumnsClause<TE, T> columns);
    }
}
