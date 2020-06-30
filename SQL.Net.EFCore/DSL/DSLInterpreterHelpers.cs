using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Streamx.Linq.SQL.Grammar;

namespace Streamx.Linq.SQL.EFCore.DSL {
    partial class DSLInterpreter {

        interface IValueHolder {
            Object Value { get; }
        }
        
        class DynamicConstant : ISequence<char>, IValueHolder {
            private ISequence<char> _string;

            private readonly object value;
            private readonly DSLInterpreter dsl;
            public DynamicConstant(object value, DSLInterpreter dsl) {
                this.value = value;
                this.dsl = dsl;
            }

            private void lazyInit() {
                if (_string == null) {
                    if (value is IEnumerable<char> || value is char) {
                        // wrap with quotes and escape existing quotes
                        var @out = new StringBuilder().Append(SINGLE_QUOTE_CHAR);
                        if (value is char c)
                            @out.Append(c);
                        else
                            @out.Append(value);
                        for (int i = @out.Length - 1;
                            i > 0;
                            i--) {
                            if (@out[i] == SINGLE_QUOTE_CHAR)
                                @out.Insert(i, SINGLE_QUOTE_CHAR);
                        }

                        _string = @out.Append(SINGLE_QUOTE_CHAR).AsSequence();
                    }
                    else if (value is decimal d) {
                        _string = Convert.ToString(decimal.ToDouble(d), CultureInfo.InvariantCulture).AsSequence();
                    }
                    else {
                        var toString = Convert.ToString(value);
                        var formatter = value?.GetType().GetCustomAttribute<ToStringFormatterAttribute>();
                        if (formatter != null)
                            toString = formatter.Replace(toString);
                        else if (value?.GetType().IsDefined(typeof(LiteralAttribute)) ?? false)
                            toString = SINGLE_QUOTE_CHAR + toString + SINGLE_QUOTE_CHAR;
                        _string = toString.AsSequence();
                    }
                }
            }

            /*public ISequence<char> registerAsParameter() {
                return dsl.registerParameter(value);
            }*/

            public IEnumerator<char> GetEnumerator() {
                return _string.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            public bool IsEmpty => value is String s && s.Length == 0;
            public Object Value => value;

            public int Length {
                get {
                    lazyInit();
                    return _string.Length;
                }
            }

            public char this[int index] {
                get {
                    lazyInit();
                    return _string[index];
                }
            }

            public ISequence<char> SubSequence(int start, int end) {
                lazyInit();
                return _string.SubSequence(start, end);
            }

            public override string ToString() {
                lazyInit();
                return _string.ToString();
            }
        }

        sealed class PackedInitializers : ISequence<char> {
            private IList<Delegate> _compiled;
            private IList<ISequence<char>> itDecoded;

            public PackedInitializers(IList<Expression> expressions, IList<Func<ISequence<char>>> producers, IList<ISequence<char>> it,
                DSLInterpreter dsl) {
                this.expressions = expressions;
                this.producers = producers;
                this.it = it;
                this.dsl = dsl;
            }

            public IList<Expression> expressions { get; }
            private IList<Func<ISequence<char>>> producers { get; }
            private IList<ISequence<char>> it { get; }
            private DSLInterpreter dsl { get; }

            public IList<ISequence<char>> getInitializers() {
                if (itDecoded != null)
                    return itDecoded;
                return itDecoded = getInitializers(it, 0);
            }

            public IList<ISequence<char>> getInitializers(ISequence<char> seq,
                int limit) {
                ISequence<char>[] seqs = new ISequence<char>[it.Count];
                seqs.Fill(seq);
                return getInitializers(seqs.ToList(), limit);
            }

            public IList<ISequence<char>> getInitializers(Object value,
                int limit) {
                if (limit <= 0)
                    limit = producers.Count + limit;

                return producers.Take(limit).Select(p => p()).ToList();
                
                // Object[] t = {value};
                // return compiled().Take(limit)
                //     .Select(f => f.DynamicInvoke(t))
                //     .Select(p => dsl.registerParameter(p))
                //     .ToList();
            }

            private IList<ISequence<char>> getInitializers(//List<ISequence<char>> it,
                int limit) {
                if (limit <= 0)
                    limit = producers.Count + limit;
                return producers.Take(limit).Select(pe => pe()).ToList();
            }

            private IList<Delegate> compiled() {
                if (_compiled == null) {
                    // var param = (ParameterExpression)arguments[0];
                    // _compiled = arguments.Skip(1).Select(_ => Expression.Lambda(_, param).Compile()).ToList();
                    _compiled = expressions.Select(_ => Expression.Lambda(_).Compile()).ToList();
                }
                    // throw new NotImplementedException();
                // compiled = expressions
                //         .Select(e => ((InvocationExpression) e).Expression)
                //         .map(ie => ie is LambdaExpression ? ((LambdaExpression) ie).Compile()
                //                 : LambdaExpression.compile(ie))
                //         .collect(Collectors.toList());

                return _compiled;
            }

            public bool IsEmpty => false;

            public int Length => throw new InvalidOperationException();

            public char this[int index] =>
                throw new InvalidOperationException();

            public ISequence<char> SubSequence(int start, int end) =>
                throw new InvalidOperationException();

            public override String ToString() => throw new InvalidOperationException();

            public IEnumerator<char> GetEnumerator() => throw new InvalidOperationException();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        internal sealed class ParameterRef : DelegatedSequence, IValueHolder {
            private ISequence<char> seq;
            public Object Value { get; }
            private readonly IList<Object> indexedParameters;
            private readonly bool isCollection;

            public ParameterRef(object value, IList<object> indexedParameters, bool isCollection) {
                this.Value = value;
                this.indexedParameters = indexedParameters;
                this.isCollection = isCollection;
            }

            public override ISequence<char> Wrapped {
                get {
                    if (seq == null) {
                        
                        var b = new StringBuilder();

                        if (isCollection) {
                            b.Append(LEFT_PARAN);
                            if (Value != null) {
                                var starting = b.Length;
                                foreach (var item in (IEnumerable)Value) {
                                    var index = indexedParameters.Count;
                                    indexedParameters.Add(item);
                                    b.Append(LEFT_BRACE).Append(index).Append(RIGHT_BRACE + COMMA);
                                }

                                if (b.Length > starting)
                                    b.Length--; //remove last comma
                            }
                            b.Append(RIGHT_PARAN);
                        }
                        else {
                            var index = indexedParameters.Count;
                            indexedParameters.Add(Value);
                            b.Append(LEFT_BRACE).Append(index).Append(RIGHT_BRACE);
                        }

                        seq = b.AsSequence();
                    }

                    return seq;
                }
            }

            public override bool IsEmpty => false;
        }

        sealed class RequiresParenthesesInAS : DelegatedSequence {
            public RequiresParenthesesInAS(ISequence<char> wrapped) {
                Wrapped = wrapped;
            }

            public override ISequence<char> Wrapped { get; }
        }

        sealed class AliasedSequence : DelegatedSequence {
            public AliasedSequence(ISequence<char> wrapped, ISequence<char> alias) {
                Wrapped = wrapped;
                Alias = alias;
            }

            public override ISequence<char> Wrapped { get; }
            public ISequence<char> Alias { get; }
        }

        sealed class View {

            private ISequence<char> columns;
            private readonly IList<string> allColumns;
            private readonly IList<Expression> arguments;
            private readonly DSLInterpreter _dsl;
            private ISequence<char> selfSelect;
            private IList<Delegate> _compiled;
            
            public View(IList<string> allColumns, IList<Expression> arguments, DSLInterpreter dsl) {
                this.allColumns = allColumns;
                this.arguments = arguments;
                _dsl = dsl;
            }

            public string getColumnLabel(int i) => allColumns[i];

            public ISequence<char> getColumn(IEnumerable<char> p,
                int i) {

                try {
                    if (i < 0)
                        i += allColumns.Count;
                    return new StringBuilder().Append(p).Append(SEP_AS_SEP).Append(allColumns[i]).AsSequence();
                }
                catch (ArgumentOutOfRangeException ioobe) {
                    throw TranslationError.ALIAS_NOT_SPECIFIED.getError(ioobe, i);
                }
            }

            private String join(IEnumerable<string> columns, bool aliased) {
                var it = aliased ? columns.Select(getColumn).Select(_ => _.ToString()) : columns;
                return String.Join(COMMA + KEYWORD_DELIMITER, it);
            }

            public ISequence<char> getSelect() {
                if (selfSelect != null)
                    return selfSelect;
                return selfSelect = join(allColumns, false).AsSequence();
            }

            public ISequence<char> getSelect(ISequence<char> seq,
                int limit,
                bool aliased) { //packed.getInitializers(seq, limit)
                if (limit <= 0)
                    limit = allColumns.size() + limit;
                return join(allColumns.Select(col => seq + DOT + col).Take(limit), aliased).AsSequence();
            }

            public ISequence<char> getSelect(Object value,
                int limit,
                bool aliased) {
                return join(getInitializers(value, limit), aliased).AsSequence();
            }
            
            public IEnumerable<string> getInitializers(Object value,
                int limit) {
                if (limit <= 0)
                    limit = allColumns.size() + limit;
                Object[] t = {value};
                return compiled().Take(limit)
                    .Select(f => f.DynamicInvoke(t))
                    .Select(p => _dsl.registerParameter(p).ToString());
            }

            private IList<Delegate> compiled() {

                if (_compiled != null)
                    return _compiled;
                
                var me = arguments[0];
                var args = arguments.Skip(1);
                if (!(me is ParameterExpression param)) {
                    param = Expression.Parameter(me.Type);
                    args = args.Select(_ => new VariableInstaller(me, param).Visit(_));
                }
                
                return _compiled = args.Select(_ => Expression.Lambda(_, param).Compile()).ToList();
            }
        }
        
        sealed class VariableInstaller : ExpressionVisitor {
            private readonly Expression target;
            private readonly ParameterExpression parameter;
            public VariableInstaller(Expression target, ParameterExpression parameter) {
                this.target = target;
                this.parameter = parameter;
            }

            public override Expression Visit(Expression node) {
                return Equals(node, target) ? parameter : base.Visit(node);
            }

            private static bool Equals(Expression node, Expression target) {
                if (node.NodeType != target.NodeType)
                    return false;

                switch (node) {
                    case MemberExpression me:
                        var tme = (MemberExpression) target;
                        return me.Member == tme.Member && Equals(me.Expression, tme.Expression);
                }

                return node == target;
            }
        }

        static String getOperatorSign(ExpressionType expressionType) {
            switch (expressionType) {
                case ExpressionType.Add:
                    return "+";
                case ExpressionType.And:
                    return "&";
                case ExpressionType.AndAlso:
                    return "AND";
                case ExpressionType.Or:
                    return "|";
                case ExpressionType.OrElse:
                    return "OR";
                case ExpressionType.Divide:
                    return "/";
                case ExpressionType.ExclusiveOr:
                    return "^";
                case ExpressionType.GreaterThan:
                    return ">";
                case ExpressionType.GreaterThanOrEqual:
                    return ">=";
                case ExpressionType.LeftShift:
                    return "<<";
                case ExpressionType.NotEqual:
                    return "<>";
                case ExpressionType.LessThan:
                    return "<";
                case ExpressionType.LessThanOrEqual:
                    return "<=";
                case ExpressionType.Modulo:
                    return "%";
                case ExpressionType.Multiply:
                    return "*";
                case ExpressionType.RightShift:
                    return ">>";
                case ExpressionType.Subtract:
                    return "-";
                default:
                    return expressionType.ToString();
            }
        }

        static ISequence<char> renderBinaryOperator(ISequence<char> lseq,
            String op,
            ISequence<char> rseq) {
            return new StringBuilder().Append(DSLInterpreter.LEFT_PARAN).Append(verifyParentheses(lseq))
                .Append(DSLInterpreter.KEYWORD_DELIMITER_CHAR)
                .Append(op)
                .Append(DSLInterpreter.KEYWORD_DELIMITER_CHAR)
                .Append(verifyParentheses(rseq))
                .Append(DSLInterpreter.RIGHT_PARAN).AsSequence();
        }

        static ISequence<char> verifyParentheses(ISequence<char> seq) {
            return SubQueryManager.isSubQueryExpression(seq)
                ? new StringBuilder().Append(DSLInterpreter.LEFT_PARAN).Append(seq).Append(DSLInterpreter.RIGHT_PARAN).AsSequence()
                : seq;
        }

        static bool isLambda(Object e) {
            if (e is LambdaExpression)
                return true;

            return e is ConstantExpression && isLambda(((ConstantExpression) e).Value);
        }

        static T getAnnotation<T>(IEnumerable<Attribute> annotations) where T : Attribute {
            foreach (var a in annotations) {
                if (a is T attribute)
                    return attribute;
            }

            return null;
        }

        static ParameterContext? calculateContext(ContextAttribute context,
            ParameterContext FuncContext) {
            if (context != null)
                return context.Value;

            return FuncContext == ParameterContext.Inherit ? (ParameterContext?) null : FuncContext;
        }

        static IList<ISequence<char>> expandVarArgs(IList<ISequence<char>> pp) {
            int lastIndex = pp.Count - 1;
            if (lastIndex < 0)
                return pp;

            ISequence<char> last = pp[lastIndex];
            if (!(last is PackedInitializers))
                return pp;

            IList<ISequence<char>> initializers = ((PackedInitializers) last).getInitializers();
            if (lastIndex == 0)
                return initializers;
            return pp.Take(lastIndex).Concat(initializers).ToList();
        }

        static IList<ISequence<char>> expandVarArgs(IList<ISequence<char>> pp,
            IList<Func<Func<ISequence<char>, ISequence<char>>>> argsBuilder,
            IList<Func<ISequence<char>, ISequence<char>>> argsBuilderBound) {
            int lastIndex = pp.Count - 1;

            ISequence<char> lastSeq = lastIndex < 0 ? null : pp[lastIndex];
            if (!(lastSeq is PackedInitializers)) {
                argsBuilderBound.AddRange(argsBuilder.Select(b => b()));
                return pp;
            }

            PackedInitializers packed = (PackedInitializers) lastSeq;
            IList<ISequence<char>> initializers = packed.getInitializers();
            pp = pp.GetRange(0, pp.Count);
            pp.RemoveAt(lastIndex);
            pp.AddRange(initializers);

            for (int i = 0; i < lastIndex; i++)
                argsBuilderBound.Add(argsBuilder[i]());

            var varargsBuilder = argsBuilder[lastIndex];
            for (int i = 0; i < initializers.Count; i++)
                argsBuilderBound.Add(varargsBuilder()); //TODO: packed.expressions[i]

            return pp;
        }

        static ISequence<char> extractColumnName(ISequence<char> expression) {
            int dotIndex = Strings.lastIndexOf(expression, DSLInterpreter.DOT_CHAR);
            if (dotIndex >= 0)
                expression = expression.SubSequence(dotIndex + 1, expression.Length);
            if (expression[0] == DSLInterpreter.SINGLE_QUOTE_CHAR)
                expression = expression.SubSequence(1, expression.Length - 1); // remove quotes coming from constant
            return expression;
        }

        Expression bind(
            Expression e) {
            while (e is ParameterExpression param) {
                // int index = param.getIndex();
                // if (index < args.Count) {
                if (!parameters.TryGetValue(param, out var x))
                        break;
                    
                e = x;

                    // var boundExpressionType = bound.NodeType;
                    // if (boundExpressionType == ExpressionType.Parameter || boundExpressionType == ExpressionType.Constant) {
                    //     if ((bound.Type != typeof(Object))
                    //         && (bound.Type != e.Type))
                    //         return boundExpressionType == ExpressionType.Constant
                    //             ? Expressions.Parameter(bound.Type, index)
                    //             : bound;
                    // }
                    // else {
                    //     e = bound;
                    // }
                    // }
            }

            return e;
        }

        private void AddFlat(IList<Expression> target, Expression a) {
            if (a is NewExpression ne && typeof(ITuple).IsAssignableFrom(a.Type))
                foreach (var e in ne.Arguments)
                    AddFlat(target, bind(e));
            else
                target.Add(a);
        }
        
        private IList<Expression> bind(
            IList<Expression> curArgs) {

            if (!curArgs.IsEmpty()) {
                var newArgs = new List<Expression>(curArgs.Count);
                for (int i = 0; i < curArgs.Count; i++) {
                    var a = bind(curArgs.get(i));

                    if (a is NewArrayExpression nae) {
                        foreach (var e in nae.Expressions)
                            newArgs.Add(bind(e));
                    }
                    else
                        AddFlat(newArgs, a);

                    // if (a is ParameterExpression) {
                    //     int index = ((ParameterExpression) a).getIndex();
                    //     if (index >= eargs.size())
                    //         continue;
                    //     Expression bound = eargs.get(index);
                    //     var boundExpressionType = bound.getExpressionType();
                    //     if (boundExpressionType == ExpressionType.Parameter
                    //         || boundExpressionType == ExpressionType.Constant) {
                    //         if ((bound.getResultType() == typeof(Object))
                    //             || (bound.getResultType() == a.getResultType()))
                    //             continue;
                    //
                    //         if (boundExpressionType == ExpressionType.Constant)
                    //             bound = Expressions.parameter(bound.getResultType(), index);
                    //     }
                    //
                    //     curArgs.set(i, bound);
                    // }
                }

                curArgs = newArgs;
            }

            return curArgs;
        }

        static ISequence<char> getAliased(ISequence<char> seq,
            IDictionary<ISequence<char>, ISequence<char>> aliases) {
            ISequence<char> label = aliases.get(seq);
            return label ?? seq;
        }

        static T GetCustomAttribute<T>(MethodBase mi) where T : Attribute => 
            (mi.IsSpecialName ? (MemberInfo) GetPropertyInfo(mi) ?? mi : mi).GetCustomAttribute<T>();

        static bool IsDefined<T>(MethodBase mi) where T : Attribute =>
            (mi.IsSpecialName ? (MemberInfo) GetPropertyInfo(mi) ?? mi : mi).IsDefined(typeof(T));

        static string GetName(MethodBase mi) => mi.IsSpecialName ? mi.Name.Substring(3) : mi.Name;
    }
}