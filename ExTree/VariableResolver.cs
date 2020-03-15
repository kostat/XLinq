using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Streamx.Linq.ExTree {
    partial class MethodVisitor {
        sealed class VariableResolver : ExpressionVisitor {
            private readonly IList<ParameterExpression> _params;
            private readonly IList<Expression> _arguments;
            
            public VariableResolver(IList<ParameterExpression> @params, IList<Expression> arguments) {
                _params = @params;
                _arguments = arguments;
            }

            protected override Expression VisitParameter(ParameterExpression node) {
                if (!_arguments.Any())
                    return node;
                return _arguments[_params.IndexOf(node)];
            }
        }
    }
}
