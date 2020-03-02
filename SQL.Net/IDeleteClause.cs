using System;
using Streamx.Linq.SQL.Grammar;

namespace Streamx.Linq.SQL {
    public interface IDeleteClause : IClause {
        [Function(OmitParentheses = true, ParameterContext = ParameterContext.FromWithoutAlias,
            ParameterContextCapabilities = new[] {nameof(Capability.ALIAS_DELETE)})]
        IDeleteUsing FROM(Object tableReference);
    }

    public interface IDeleteUsing : IClause {
        [Function(OmitParentheses = true, ParameterContext = ParameterContext.FromWithoutAlias,
            ParameterContextCapabilities = new[] {nameof(Capability.ALIAS_DELETE)})]
        IClause USING(params Object[] tableReferences);
    }
}
