using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.EntityFrameworkCore.Metadata;
using Streamx.Linq.SQL.Grammar;

namespace Streamx.Linq.SQL.EFCore.DSL {
    partial class DSLInterpreter : ExpressionVisitor {
        private const string DTYPE = "DTYPE";

        private const char UNDERSCORE_CHAR = '_';
        private const string COMMA = ",";
        private const string AS = "AS";
        private const string NEW_LINE = "\n";
        private const string STAR = "*";
        private const char QUESTION_MARK_CHAR = '?';
        private const char DOT_CHAR = IdentifierPath.DOT;
        private const string DOT = ".";
        private const string AND = "AND";
        private const string EQUAL_SIGN = "=";
        private const string NOT_EQUAL_SIGN = "<>";
        private const string KEYWORD_DELIMITER = " ";
        private const string SEP_AS_SEP = KEYWORD_DELIMITER + AS + KEYWORD_DELIMITER;
        private const char KEYWORD_DELIMITER_CHAR = ' ';
        private const string TABLE_ALIAS_PREFIX = "t";
        private const string SUB_QUERY_ALIAS_PREFIX = "q";
        private const string LEFT_PARAN = "(";
        private const string RIGHT_PARAN = ")";
        private const string LEFT_BRACE = "{";
        private const string RIGHT_BRACE = "}";
        private const char SINGLE_QUOTE_CHAR = '\'';
        private const string IS_NULL = "IS NULL";
        private const string IS_NOT_NULL = "IS NOT NULL";
        private const string NULL = "NULL";

        private SubQueryManager subQueries_ = new SubQueryManager(Collections.emptyList<SubQueryManager.SubQuery>());

        // PRIMARY value - primary, empty string - the only secondary, otherwise - named secondary
        private static readonly String PRIMARY =
#if NETFRAMEWORK
            String.Copy("PRIMARY");
#else
            new String("PRIMARY");
#endif
        private readonly IDictionary<ISequence<char>, String> tableRefs = new Dictionary<ISequence<char>, String>();
        private IDictionary<ISequence<char>, IDictionary<String, ISequence<char>>> tableSecondaryRefs = Collections.emptyDictionary<ISequence<char>, IDictionary<String, ISequence<char>>>();
        private IDictionary<ISequence<char>, ISequence<char>> aliases_ = Collections.emptyDictionary<ISequence<char>, ISequence<char>>();
        private ISequence<char> subQueryAlias;
        private IList<ISequence<char>> undeclaredAliases = Collections.emptyList<ISequence<char>>();
        private Expression argumentsTarget; // used to differentiate invocation vs. reference

        // join table
        /*private IDictionary<ParameterExpression, MemberInfo> joinTables = Collections.emptyDictionary<ParameterExpression, MemberInfo>();
        private IDictionary<ISequence<char>, MemberInfo> joinTablesForFROM = Collections.emptyDictionary<ISequence<char>, MemberInfo>();
        private IDictionary<ParameterExpression, MemberInfo> collectionTables = Collections.emptyDictionary<ParameterExpression, MemberInfo>();
        private IDictionary<ISequence<char>, MemberInfo> collectionTablesForFROM = Collections.emptyDictionary<ISequence<char>, MemberInfo>();*/

        private readonly IDictionary<ParameterExpression, ParameterExpression>
            parameterBackwardMap = new Dictionary<ParameterExpression, ParameterExpression>();
        
        private IDictionary<Expression, Func<ISequence<char>>> selfToType = Collections.emptyDictionary<Expression, Func<ISequence<char>>>();

        // View
        private IDictionary<ISequence<char>, View> views = Collections.emptyDictionary<ISequence<char>, View>();

        private readonly IList<Object> indexedParameters = new List<Object>();
        private readonly IDictionary<(object, FieldInfo), ISequence<char>> cachedParameters = new Dictionary<(object, FieldInfo), ISequence<char>>();
        private readonly IDictionary<ParameterExpression, Expression> parameters = new Dictionary<ParameterExpression, Expression>();
        private readonly IDictionary<Expression, ISequence<char>> parameterResults = new Dictionary<Expression, ISequence<char>>();
        private IDictionary<object, IDictionary<MemberInfo, ISequence<char>>> contexts =
            Collections.emptyDictionary<object, IDictionary<MemberInfo, ISequence<char>>>();
        private IDictionary<ISequence<char>, View> tupleViewMap = Collections.emptyDictionary<ISequence<char>, View>();
        private View evaluationContextView;

        private ParameterContext renderingContext = ParameterContext.Expression;

        private int parameterCounter;
        private int subQueriesCounter;
        private bool renderingAssociation;

        private readonly IModel model;

        public DSLInterpreter(IModel model) {
            this.model = model;
        }

        public IList<object> IndexedParameters => indexedParameters;

        private void addUndeclaredAlias(ISequence<char> alias) {
            if (undeclaredAliases.IsEmpty())
                undeclaredAliases = new List<ISequence<char>>();

            undeclaredAliases.Add(alias);
        }

        private ParameterContext getParameterContext(FunctionAttribute f,
            CommonTableExpressionAttribute cte) {
            foreach (var scap in f.ParameterContextCapabilities) {
                var cap = Capability.Parse(scap);
                if (ExLINQ.Capabilities.Contains(cap))
                    return cap.Hint<ParameterContext>();
            }

            return f.ParameterContext;
        }

        protected SubQueryManager getSubQueries() {
            return subQueries_;
        }

        protected IDictionary<ISequence<char>, ISequence<char>> getAliases() {
            return aliases_;
        }

        protected override Expression VisitBinary(BinaryExpression node) {
            return visit(node).AsXExpression();
        }

        private Func<Func<ISequence<char>>> visit(BinaryExpression e) {
            return () => {
                Expression first = bind(e.Left);
                Expression second = bind(e.Right);

                var efirst = this.Visit(first).As<Func<Func<ISequence<char>>>>();
                var esecond = this.Visit(second).As<Func<Func<ISequence<char>>>>();

                var left = efirst();
                var right = esecond();

                bool isAssociation(Expression first, Expression second) =>
                    (isEntityLike(first.Type) || isCollection(first.Type)) && (isEntityLike(second.Type) || isCollection(second.Type));
                
                var aliases = getAliases();
                switch (e.NodeType) {
                    case ExpressionType.Equal:
                        return () => {
                            var isAssoc = isAssociation(first, second);

                            if (isAssoc)
                                renderingAssociation = true;
                            var lseq = left();
                            var rseq = right();
                            if (isAssoc) {
                                renderingAssociation = false;
                                return renderAssociation(new StringBuilder(), getAssociation(first, second), aliases, lseq,
                                    rseq, true);
                            }

                            if (lseq == null || Equals(NULL, lseq.ToString()))
                                return rseq.IsNull();
                            
                            if (rseq == null || Equals(NULL, rseq.ToString()))
                                return lseq.IsNull();

                            return renderBinaryOperator(lseq, EQUAL_SIGN, rseq);
                        };

                    case ExpressionType.NotEqual:
                        return () => {
                            var isAssoc = isAssociation(first, second);

                            if (isAssoc)
                                renderingAssociation = true;
                            var lseq = left();
                            var rseq = right();
                            if (isAssoc) {
                                renderingAssociation = false;
                                return renderAssociation(new StringBuilder(), getAssociation(first, second), aliases, lseq,
                                    rseq, false);
                            }
                            
                            if (lseq == null || Equals(NULL, lseq.ToString()))
                                return rseq.IsNotNull();
                            
                            if (rseq == null || Equals(NULL, rseq.ToString()))
                                return lseq.IsNotNull();
                            
                            var op = getOperatorSign(e.NodeType);
                            return renderBinaryOperator(lseq, op, rseq);
                        };
                    case ExpressionType.Add:
                    case ExpressionType.And:
                    case ExpressionType.Or:
                    case ExpressionType.Divide:
                    case ExpressionType.ExclusiveOr:
                    case ExpressionType.GreaterThan:
                    case ExpressionType.GreaterThanOrEqual:
                    case ExpressionType.LeftShift:
                    case ExpressionType.LessThan:
                    case ExpressionType.LessThanOrEqual:
                    case ExpressionType.AndAlso:
                    case ExpressionType.OrElse:
                    case ExpressionType.Modulo:
                    case ExpressionType.Multiply:
                    case ExpressionType.RightShift:
                    case ExpressionType.Subtract:
                        return () => {
                            var lseq = left();
                            var rseq = right();
                            var op = getOperatorSign(e.NodeType);
                            return renderBinaryOperator(lseq, op, rseq);
                        };
                    case ExpressionType.Assign:
                        switch (e.Left) {
                            case ParameterExpression param:
                                return () => {
                                    var result = right();
                                    if (param.Type.IsDefined(typeof(NoOpAttribute))) {
                                        parameterResults[param] = null;
                                        return result;
                                    }
                                    else {
                                        parameterResults[param] = result;
                                        return null;
                                    }
                                };
                            case MemberExpression me:
                                if (me.Expression is ConstantExpression cme) {

                                    if (e.Right is ConstantExpression rce) {
                                        // var lambda = Expression.Lambda(e, Collections.emptyList<ParameterExpression>());
                                        // lambda.Compile().DynamicInvoke();
                                        ((FieldInfo)me.Member).SetValue(cme.Value, rce.Value);

                                        return () => null;
                                    }
                                    
                                    return () => {

                                        if (contexts.IsEmpty())
                                            contexts = new Dictionary<object, IDictionary<MemberInfo, ISequence<char>>>();

                                        if (!contexts.TryGetValue(cme.Value, out var map)) {
                                            map = new Dictionary<MemberInfo, ISequence<char>>();
                                            contexts[cme.Value] = map;
                                        }

                                        evaluationContextView = null;

                                        var result = right();
                                        map[me.Member] = result;

                                        if (typeof(ITuple).IsAssignableFrom(me.Type) && evaluationContextView != null) {
                                            if (tupleViewMap.IsEmpty())
                                                tupleViewMap = new Dictionary<ISequence<char>, View>();

                                            tupleViewMap[result] = evaluationContextView;
                                        }

                                        return null;
                                    };
                                }
                                
                                break;
                        }

                        goto default;
                    default:
                        throw TranslationError.UNSUPPORTED_EXPRESSION_TYPE
                            .getError(getOperatorSign(e.NodeType));
                }
            };
        }

        private ISequence<char> renderAssociation(StringBuilder @out,
            Association assoc,
            IDictionary<ISequence<char>, ISequence<char>> aliases,
            ISequence<char> lseq,
            ISequence<char> rseq,
            bool equals) {
            @out.Append(LEFT_PARAN);

            for (int i = 0; i < assoc.Cardinality; i++) {
                if (@out.Length > 1)
                    @out.Append(KEYWORD_DELIMITER + AND + KEYWORD_DELIMITER);

                if (Equals(lseq.ToString(), NULL)) {
                    appendSide(rseq, assoc.Right);
                    @out.Append(KEYWORD_DELIMITER + (equals ? IS_NULL : IS_NOT_NULL));
                    continue;
                }
 
                appendSide(lseq, assoc.Left);
                
                if (Equals(rseq.ToString(), NULL)) {
                    @out.Append(KEYWORD_DELIMITER + (equals ? IS_NULL : IS_NOT_NULL));
                    continue;
                }

                @out.Append(KEYWORD_DELIMITER + (equals ? EQUAL_SIGN : NOT_EQUAL_SIGN) + KEYWORD_DELIMITER);

                appendSide(rseq, assoc.Right);

                void appendSide(ISequence<char> sequence, IList<ISequence<char>> side) {
                    if (IdentifierPath.isResolved(sequence))
                        @out.Append(sequence);
                    else
                        @out.Append(resolveLabel(aliases, sequence)).Append(DOT).Append(side.get(i));
                }
            }

            return @out.Append(RIGHT_PARAN).AsSequence();
        }

        protected override Expression VisitConstant(ConstantExpression node) {
            return visit(node).AsXExpression();
        }

        Func<Func<ISequence<char>>> visit(ConstantExpression e) {
            Object value = e.Value;

            if (value == null)
                return () => () => NULL.AsSequence();
            
            if (value is Expression) {
                return () => {
                    Expression ex = (Expression) value;
                    argumentsTarget = ex;
                    return this.Visit(ex).As<Func<Func<ISequence<char>>>>()();
                };
            }

            return () => {
                if (value is object[] objects && objects.Length == 0)
                    return () => new PackedInitializers(Collections.emptyList<Expression>(), Collections.emptyList<Func<ISequence<char>>>(),
                        Collections.emptyList<ISequence<char>>(), this);

                // if (!(value is IKeyword) && (collectingParameters ?? false))
                //     return () => registerParameter(value);

                return () => new DynamicConstant(value, this);
            };
        }

        private ISequence<char> registerParameter(object value, bool isCollection = false) {
            return new ParameterRef(value, indexedParameters, isCollection);
        }
        
        private ISequence<char> registerParameter(object obj, MemberInfo member) {
            var field = (FieldInfo) member;
            var key = (obj, field);
            if (cachedParameters.TryGetValue(key, out var seq))
                return seq;
            var value = field.GetValue(obj);
            
            bool isCollecton = Type.GetTypeCode(field.FieldType) == TypeCode.Object &&
                               typeof(IEnumerable).IsAssignableFrom(field.FieldType);
            return cachedParameters[key] = registerParameter(value, isCollecton);
        }

       /* protected override Expression VisitInvocation(InvocationExpression node) {
            return visit(node).AsXExpression();
        }

        //TODO: MethodCallExpression
        public Func<Func<ISequence<char>>> visit(InvocationExpression e) {
            MemberExpression target = (MemberExpression) e.Expression;
            var ftarget = this.Visit(target).As<Func<Func<ISequence<char>>>>();

            var allArgs = e.Arguments;

            // Stream<Func<List<Expression>, Func<List<ISequence<char>>, ISequence<char>>>>
            var args = allArgs
                .Select(p => isLambda(p) ? () => () => null : this.Visit(p).As<Func<Func<ISequence<char>>>>());

            return () => {
                bool isSubQuery = false;
                ISequence<char> previousSubQueryAlias = null;

                if (target.NodeType == ExpressionType.Call) {
                    MemberInfo method = target.Member;
                    isSubQuery = method.IsDefined(typeof(SubQueryAttribute));

                    if (isSubQuery) {
                        previousSubQueryAlias = subQueryAlias;
                        subQueryAlias = new StringBuilder().Append(SUB_QUERY_ALIAS_PREFIX).Append(subQueriesCounter++).AsSequence();
                    }
                }

                // only for the first time
                if (collectingParameters.HasValue)
                    collectingParameters = true;
                var fargs = args.Select(arg => arg()).ToList();
                collectingParameters = null;

                // Func<IList<ISequence<char>>> @params = () => fargs
                //     .Select(arg => arg()).ToList();

                // IList<Expression> curArgs = bind(allArgs.ToList());

                argumentsTarget = target;
                Func<ISequence<char>> fmember = ftarget();

                if (target.getExpressionType() == ExpressionType.Lambda) {
                    return fmember;
                }

                // Func<IList<Expression>, Func<IList<ISequence<char>>, Func<IList<ISequence<char>>, ISequence<char>>>> m1 =
                //     (Func<IList<Expression>, Func<IList<ISequence<char>>, Func<IList<ISequence<char>>, ISequence<char>>>>) (Object) fmember;
                //
                // Func<IList<ISequence<char>>, Func<IList<ISequence<char>>, ISequence<char>>> m = m1(eargs);

                if (isSubQuery) {
                    subQueryAlias = previousSubQueryAlias;
                }

                return fmember;
            };
        }*/

        private IList<Expression> prepareLambdaParameters(IList<ParameterExpression> declared,
            IList<Expression> arguments) {
            IList<Expression> result = new List<Expression>(declared);
            for (int i = 0; i < arguments.size(); i++) {
                if (i >= declared.size())
                    break;
                Expression original = arguments.get(i);
                Expression arg = original;
                while (arg is UnaryExpression)
                    arg = ((UnaryExpression) arg).Operand;

                if (arg.getResultType() == typeof(Object))
                    continue; // better leave parameter

                // don't forward anything except LambdaExpression
                if (arg is ConstantExpression) {
                    if (!(((ConstantExpression) arg).Value is LambdaExpression))
                        arg = Expressions.parameter(original.getResultType(), i);
                }
                else if (!(arg is LambdaExpression)) {
                    ParameterExpression newParam = Expressions.parameter(original.getResultType(), i);
                    if (arg is ParameterExpression)
                        parameterBackwardMap.put(newParam, (ParameterExpression) arg);
                    arg = newParam;
                }

                result.set(i, arg);
            }

            return result;
        }

        protected override Expression VisitLambda<T>(Expression<T> node) {
            return visit(node).AsXExpression();
        }

        public Func<Func<ISequence<char>>> visit(LambdaExpression e) {
            bool allocateScope = e.ReturnType == typeof(void) ||
                                 e.ReturnType.IsDefined(typeof(NoOpAttribute));

            var ffparams = e.Parameters
                .Select(p => this.Visit(p).As<Func<Func<ISequence<char>>>>()).ToList();
            var ff = this.Visit(e.Body).As<Func<Func<ISequence<char>>>>();
            // IEnumerable<Func<List<Expression>, Func<List<ISequence<char>>, ISequence<char>>>> flocals = e.getLocals()
            //         .stream()
            //         .map(l -> l != null ? l.accept(this) : null);


            return () => {
                
                var fparams = ffparams.Select(_ => _()).ToList();
                
                IDictionary<ISequence<char>, ISequence<char>> currentAliases;
                SubQueryManager currentSubQueries, capturedSubQueries;

                if (allocateScope) {
                    currentAliases = aliases_;
                    aliases_ = new ScopedDictionary<ISequence<char>, ISequence<char>>(currentAliases);
                    currentSubQueries = subQueries_;
                    capturedSubQueries = subQueries_ = new SubQueryManager(currentSubQueries);
                }
                else {
                    currentAliases = null;
                    currentSubQueries = capturedSubQueries = null;
                }

                try {
                    // IList<Expression> eargsFinal = argumentsTarget == e ? eargs : Collections.emptyList<Expression>();
                    argumentsTarget = e.Body;

                    // IList<Expression> eargsPrepared = prepareLambdaParameters(e.Parameters, eargsFinal);

                    // var f = ff();

                    // List<Func<List<ISequence<char>>, ISequence<char>>> ple = flocals
                    //         .map(p -> p != null ? p.apply(eargsPrepared) : null)
                    //         .collect(Collectors.toList());
                    //
                    // if (!ple.isEmpty()) {
                    //     f = f.compose(pp -> {
                    //         List<ISequence<char>> npe = new ArrayList<>(pp);
                    //         ple.forEach(le -> npe.add(le != null ? le.apply(npe) : null));
                    //         return npe;
                    //     });
                    // }
                    var x = ff()//.compose(visitParameters(e.Parameters)
                        .andThen(seq => {
                            try {
                                return seq;
                            }
                            finally {
                                if (allocateScope)
                                    capturedSubQueries.close();
                            }
                        });

                    return () => {
                        fparams.ForEach(_ => _());
                        return x();
                    };
                }
                finally {
                    if (allocateScope) {
                        aliases_ = currentAliases;
                        subQueries_ = currentSubQueries;
                    }
                }
            };
        }

        private Func<IList<ISequence<char>>> visitParameters(IList<ParameterExpression> original) {
            // IEnumerable<Func<Func<IList<ISequence<char>>> parameters,
            // IList<Expression> eargs) {
            // IList<Func<IList<ISequence<char>>, ISequence<char>>> ppe = parameters.Select(p => p()).ToArray();
            //
            // Func<IList<ISequence<char>>, IList<ISequence<char>>> @params = pp => {
            //     pp = pp.GetRange(0, eargs.size());
            //     ISequence<char>[] r = new ISequence<char>[ppe.size()];
            //
            //     for (int index = 0; index < r.Length; index++) {
            //         Func<IList<ISequence<char>>, ISequence<char>> pe = ppe.get(index);
            //         r[original.get(index).getIndex()] = pe(pp);
            //     }
            //
            //     return r;
            // };
            //
            // return @params;
            return null;
        }

        /*private Func<List<Expression>, Func<List<Expression>, Func<List<ISequence<char>>, Func<List<ISequence<char>>, ISequence<char>>>>> visitDelegateExpression(DelegateExpression e) {

            Func<Object[], ?> fdelegate = LambdaExpression.Compile(e.getDelegate());

            return invocationArguments -> instanceArguments -> {
                Expression delegate = (Expression) fdelegate.apply(instanceArguments.toArray());
                argumentsTarget = delegate;
                Func<List<ISequence<char>>, ISequence<char>> x = delegate.accept(this).apply(invocationArguments);
                return ipp -> x;
            };
        }*/

        private Func<ISequence<char>, ISequence<char>> setupParameterRenderingContext(ParameterContext? newContext,
            Func<ISequence<char>, ISequence<char>> parameterRenderer) {
            if (newContext == null)
                return parameterRenderer;

            return seq => {
                ParameterContext current = renderingContext;
                renderingContext = newContext.Value;
                try {
                    return parameterRenderer(seq);
                }
                finally {
                    renderingContext = current;
                }
            };
        }

        private ISequence<char> resolveLabel(IDictionary<ISequence<char>, ISequence<char>> aliases,
            ISequence<char> seq) {
            ISequence<char> label = aliases.get(seq);
            if (label != null)
                return label;

            return SubQueryManager.isSubQuery(seq) ? SubQueryManager.getName(seq) : seq;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node) {
            return visitMemberExpression(node).AsXExpression();
        }

        protected override Expression VisitInvocation(InvocationExpression node) {
            
            var boundArgs = bind(node.Arguments);
            var boundArgs0 = boundArgs.Select(Visit).Select(_ => _.As<Func<Func<ISequence<char>>>>());
            var expr0 = Visit(node.Expression).As<Func<Func<ISequence<char>>>>();

            var @params = ((LambdaExpression) node.Expression).Parameters;
            
            Func<Func<ISequence<char>>> f = () => {
                var boundArgs1 = boundArgs0.Select(_ => _()).ToList();
                var expr1 = expr0();

                return () => {
                    boundArgs1.Select(_ => _()).ForEach((seq, i) => parameterResults[@params[i]] = seq);
                    return expr1();
                };
            };
            
            return f.AsXExpression();
        }

        protected override Expression VisitMember(MemberExpression node) {

            var original = node;
            if (node.Expression is MemberExpression me && typeof(ITuple).IsAssignableFrom(me.Type))
                node = me;

            var lambda = Expression.Lambda(node.Expression);
            var value  = lambda.Compile().DynamicInvoke();

            Func<Func<ISequence<char>>> f = () => {
                var aliases = getAliases();
                return () => {
                    if (!contexts.TryGetValue(value, out var map))
                        return registerParameter(value, node.Member);

                    if (!map.TryGetValue(node.Member, out var seq))
                        return null;

                    if (original != node) {
                        if (!tupleViewMap.TryGetValue(seq, out var view))
                            throw TranslationError.NO_COLUMN_DEFINITION_PROVIDED.getError();
#if NETFRAMEWORK
                        var i = int.Parse(original.Member.Name.Substring(4));
#else
                        var i = int.Parse(original.Member.Name.AsSpan(4));
#endif
                        seq = (getAliased(seq, aliases) + DOT + view.getColumnLabel(i - 1)).AsSequence();
                    }

                    return seq;
                };
            };
            return f.AsXExpression();
        }

        private Func<Func<ISequence<char>>>
            visitMemberExpression(MethodCallExpression e) {

            MethodInfo m = e.Method;

            Expression ei = bind(e.Object);
            var finstance = ei != null
                ? Visit(ei).As<Func<Func<ISequence<char>>>>()
                : null;

            var boundArgs = bind(e.Arguments);
            var boundArgs0 = boundArgs.Select(Visit).Select(_ => _.As<Func<Func<ISequence<char>>>>());

            return () =>  {
                bool isSubQuery = m.IsDefined(typeof(SubQueryAttribute));
                ISequence<char> localSubQueryAlias = null;
                ISequence<char> previousSubQueryAlias = null;
                
                if (isSubQuery) {
                    previousSubQueryAlias = this.subQueryAlias;
                    localSubQueryAlias = (SUB_QUERY_ALIAS_PREFIX + subQueriesCounter++).AsSequence();
                    this.subQueryAlias = localSubQueryAlias;
                }

                var instance = finstance?.Invoke();
                
                var boundArgs1 = boundArgs0.Select(_ => _()).ToList();
                
                if (isSubQuery) {
                    this.subQueryAlias = previousSubQueryAlias;
                }

                // Alias alias = m.getAnnotation(Alias.class);
                var alias = m.GetCustomAttribute<AliasAttribute>();
                if (alias != null) {
                    if (alias.Value) {
                        if (e.Arguments.size() > 2)
                            throw new InvalidOperationException();

                        var aliases1 = getAliases();
                        
                        // return ipp => {
                            var inst = instance?.Invoke(); //ipp
                            return () => {
                                ISequence<char> aliased = inst ?? boundArgs1.First()();
                                int numOfArgs = e.Arguments.size();
                                if (numOfArgs == 1)
                                    return aliased;
                                int labelIndex = numOfArgs - 1;
                                ISequence<char> label = boundArgs1.Skip(labelIndex).First()();
                                label = extractColumnName(label);
                                aliases1.put(aliased, label);
                                return aliased;
                            };
                        // };
                    }
                    else {
                        return () => {
                            if (!boundArgs.IsEmpty())
                                expandVarArgs(boundArgs1.Select(_ => _()).ToList()).ForEach(x => addUndeclaredAlias(x));
                            return "".AsSequence();
                        };
                    }
                }

                if (m.IsDefined(typeof(TableDeclarationAttribute))) {
                    return () => {
                        var tableNameParts = boundArgs1.Select(_ => _()).ToList();
                        var tableNamePart = tableNameParts[0];
                        var tableName = (tableNamePart is IValueHolder h1 ? h1.Value : tableNamePart).ToString();
                        tableNamePart = tableNameParts[1];
                        var schema = (tableNamePart is IValueHolder h2 ? h2.Value : tableNamePart).ToString();
                        return CalcTableReference(e, ReferenceEquals(schema, NULL) ? tableName : schema + DOT + tableName);
                    };
                }

                /*if (m.IsDefined(typeof(ParameterAttribute))) { //isAnnotationPresent(Parameter.class)
                    //if (!FluentJPA.checkLicense())
                    //    throw TranslationError.REQUIRES_LICENSE.getError(Normalizer.DYNAMIC_QUERIES_FUNCTIONALITY);
                    return () => {
                        ISequence<char> seq = boundArgs1.First()();
                        if (!seq.isNullOrEmpty() && seq[0] == QUESTION_MARK_CHAR)
                            return seq;
                        if (!(seq is DynamicConstant))
                            throw TranslationError.REQUIRES_EXTERNAL_PARAMETER.getError(seq);

                        return ((DynamicConstant) seq).registerAsParameter();
                    };
                }*/

                /*TableJoin tableJoin = m.getAnnotation(TableJoin.class);
                Member tableJoinMember = (tableJoin != null)
                        ? getJoinMember(ei, invocationArguments, instanceArguments, () -> {
                            if (joinTables.isEmpty()) {
                                joinTables = new HashMap<>();
                                joinTablesForFROM = new HashMap<>();
                            }
    
                            return joinTables;
                        })
                        : null;
    
                TableCollection tableCol = m.getAnnotation(TableCollection.class);
                Member tableColMember = (tableCol != null)
                        ? getJoinMember(ei, invocationArguments, instanceArguments, () -> {
                            if (collectionTables.isEmpty()) {
                                collectionTables = new HashMap<>();
                                collectionTablesForFROM = new HashMap<>();
                            }
    
                            return collectionTables;
                        })
                        : null;*/

                SubQueryManager subQueries = getSubQueries();
                IDictionary<ISequence<char>, ISequence<char>> aliases = getAliases();

                if (m.IsDefined(typeof(AliasAttribute.UseAttribute))) {
                    return () => getAliased(boundArgs1.First()(), aliases);
                }

                var cte = m.GetCustomAttribute<CommonTableExpressionAttribute>();
                if (cte != null) {
                    switch (cte.Value) {
                        case CommonTableExpressionType.Self: {

                            var subQueryAlias1 = this.subQueryAlias;

                            if (!selfToType.TryGetValue(ei, out var self)) {
                                if (selfToType.IsEmpty())
                                    selfToType = new Dictionary<Expression, Func<ISequence<char>>>();
                                selfToType[ei] = self = visit(Expression.Parameter(e.Type))();
                            }

                            return () =>
                                subQueries.put(self(), subQueryAlias1, false);
                        }
                        case CommonTableExpressionType.Declaration:
                            return () => {
                                StringBuilder with = new StringBuilder().Append(m.Name).Append(KEYWORD_DELIMITER_CHAR);
                                int startLength = with.Length;
                                foreach (ISequence<char> subQueryAlias1 in expandVarArgs(boundArgs1.Select(_ => _()).ToList())) {
                                    if (with.Length > startLength)
                                        with.Append(COMMA);
                                    ISequence<char> subQuery = subQueries.sealName(subQueryAlias1);
                                    if (subQuery == null) {
                                        if (subQueryAlias1 is DynamicConstant) {
                                            with.Append(subQueryAlias1).Append(KEYWORD_DELIMITER_CHAR);
                                            startLength = with.Length;
                                            continue;
                                        }
                                        else
                                            throw new ArgumentException("Parameter must be subQuery: " + subQueryAlias1);
                                    }

                                    with.Append(subQueryAlias1)
                                        .Append(SEP_AS_SEP + NEW_LINE + LEFT_PARAN)
                                        .Append(subQuery)
                                        .Append(RIGHT_PARAN + NEW_LINE);
                                }

                                return with.AsSequence();
                            };
                        case CommonTableExpressionType.Reference:
                            return () => {
                                ISequence<char> seq = boundArgs1.First()();
                                return SubQueryManager.isSubQuery(seq) ? SubQueryManager.getName(seq) : seq;
                            };
                    }
                }

                //if (m.isAnnotationPresent(TableExtension.DiscriminatorFilter.class)) {
                /*if (m.IsDefined(typeof())) {
                    return ipp -> pp -> {
    
                        boolean isDiscrNumeric;
                        String discrColumnName;
                        String discrColumnValue;
    
                        Class<?> baseType = invocationArguments.get(0).getResultType();
                        DiscriminatorColumn discrColumn = baseType.getAnnotation(DiscriminatorColumn.class);
    
                        if (discrColumn != null) {
                            isDiscrNumeric = discrColumn.discriminatorType() == DiscriminatorType.INTEGER;
                            discrColumnName = discrColumn.name();
                        } else {
                            isDiscrNumeric = false;
                            discrColumnName = DTYPE;
                        }
    
                        Expression derived = invocationArguments.get(1);
                        Class<?> derivedType = derived.getResultType();
                        if (derivedType == Class.class) {
                            derivedType = (Class<?>) ((ConstantExpression) derived).getValue();
                        }
    
                        DiscriminatorValue discrValue = derivedType.getAnnotation(DiscriminatorValue.class);
                        if (discrValue != null)
                            discrColumnValue = discrValue.value();
                        else {
                            discrColumnValue = derivedType.getAnnotation(Entity.class).name();
                            if (discrColumnValue.isEmpty())
                                discrColumnValue = derivedType.getName();
                        }
    
                        StringBuilder out = new StringBuilder();
                        out.append(getAliased(pp.get(0), aliases))
                                .append(DOT_CHAR)
                                .append(discrColumnName)
                                .append(KEYWORD_DELIMITER_CHAR + EQUAL_SIGN + KEYWORD_DELIMITER_CHAR);
    
                        if (!isDiscrNumeric)
                            out.append(SINGLE_QUOTE_CHAR);
                        out.append(discrColumnValue);
                        if (!isDiscrNumeric)
                            out.append(SINGLE_QUOTE_CHAR);
    
                        return out;
    
                    };
                }*/

                var function = GetCustomAttribute<FunctionAttribute>(m);
                ParameterContext functionContext = function == null
                    ? ParameterContext.Inherit
                    : getParameterContext(function, cte);
                var parameters = m.GetParameters();
                var contextAttributes = new ContextAttribute[boundArgs.size()];

                var argsBuilder = new List<Func<Func<ISequence<char>, ISequence<char>>>>(
                    boundArgs.size());
                for (int i = 0; i < boundArgs.size(); i++) {
                    Expression arg = boundArgs.get(i);

                    var pi = i;
                    if (pi >= parameters.Length)
                        pi = parameters.Length - 1;
                    var parameterAnnotations = parameters[pi].GetCustomAttributes();
                    var contextAnnotation = getAnnotation<ContextAttribute>(parameterAnnotations);
                    contextAttributes[pi] = contextAnnotation;
                    ParameterContext? context = calculateContext(contextAnnotation, functionContext);

                    if (context == ParameterContext.From || context == ParameterContext.FromWithoutAlias)
                        argsBuilder.Add(() => setupParameterRenderingContext(context,
                            tableReference(arg, subQueries, aliases)));
                    else {
                        var literal = getAnnotation<LiteralAttribute>(parameterAnnotations);

                        argsBuilder.Add(() => {
                            Func<ISequence<char>, ISequence<char>> renderer = setupParameterRenderingContext(context,
                                expression(arg, subQueries, aliases));

                            return literal != null && literal.Quote
                                ? renderer.andThen(seq => new StringBuilder(seq.Length + 2).Append(SINGLE_QUOTE_CHAR)
                                    .Append(seq)
                                    .Append(SINGLE_QUOTE_CHAR).AsSequence())
                                : renderer;
                        });
                    }
                }

                return () => {
                    StringBuilder @out = new StringBuilder();
                    ISequence<char> instMutating = null;
                    ISequence<char> originalInst;
                    if (instance != null) {
                        // the table definition must come from JOIN clause
                        ISequence<char> inst = instance(); //ipp
                        originalInst = inst;

                        /*if (tableJoin != null) {
                            ISequence<char> lseq = inst;
                            return pp -> {
                                Association association = getAssociationMTM(tableJoinMember, tableJoin.inverse());
                                return renderAssociation(out, association, aliases, lseq, pp.get(0));
                            };
                        }*/

                        /*if (tableCol != null) {
                            ISequence<char> lseq = inst;
                            return pp -> {
                                Association association = getAssociationElementCollection(tableColMember);
                                return renderAssociation(out, association, aliases, lseq, pp.get(0));
                            };
                        }
    
                        if (m.isAnnotationPresent(TableJoin.Property.class)) {
                            ISequence<char> lseq = inst;
                            return pp -> {
                                Member member = joinTablesForFROM.get(lseq);
                                if (member == null)
                                    throw TranslationError.ASSOCIATION_NOT_INITED.getError(m);
    
                                return new IdentifierPath.MultiColumnIdentifierPath(m.getName(),
                                        clazz -> getAssociationMTM(member,
                                                !member.getDeclaringClass().isAssignableFrom(clazz)),
                                        null).resolveInstance(lseq,
                                                        tableSecondaryRefs.get(lseq));
                            };
                        }
    
                        Property tableColProperty = m.getAnnotation(TableCollection.Property.class);
                        if (tableColProperty != null) {
                            ISequence<char> lseq = inst;
                            Member member = collectionTablesForFROM.get(lseq);
                            if (member == null)
                                throw TranslationError.ASSOCIATION_NOT_INITED.getError(m);
    
                            if (tableColProperty.owner()) {
                                return pp -> new IdentifierPath.MultiColumnIdentifierPath(m.getName(),
                                        c -> getAssociationElementCollection(member), null).resolveInstance(lseq,
                                                tableSecondaryRefs.get(lseq));
                            } else {
                                return pp -> {
    
                                    Class<?> target = getTargetForEC(member);
                                    if (!isEmbeddable(target))
                                        return getColumnNameFromProperty(member).resolveInstance(lseq,
                                                tableSecondaryRefs.get(lseq));
    
                                    return calcOverrides(lseq, member, tableSecondaryRefs.get(lseq));
                                };
                            }
                        }
    
                        TableExtension tableExtension = m.getAnnotation(TableExtension.class);
                        if (tableExtension != null) {
                            return pp -> {
                                String secondary;
    
                                if (pp.size() > 1) {
                                    ISequence<char> secTableName = pp.get(1);
                                    if (!(secTableName instanceof DynamicConstant))
                                        throw TranslationError.SECONDARY_TABLE_NOT_CONSTANT.getError(secTableName);
                                    secondary = String.valueOf(((DynamicConstant) secTableName).getValue());
                                } else {
                                    secondary = null;
                                }
    
                                Expression derivedExpression = invocationArguments.get(0);
                                Class<?> derivedType = derivedExpression.getResultType();
                                ISequence<char> derived = pp.get(0);
                                Association assoc;
    
                                if (tableExtension.value() == TableExtensionType.INHERITANCE) {
                                    Class<?> base = getInheritanceBaseType(derivedType);
                                    tableRefs.put(originalInst, getTableName(base));
    
                                    registerSecondaryTable(derived, base.getName(), originalInst);
    
                                    assoc = getAssociation(derivedExpression, derivedExpression);
    
                                } else {
                                    SecondaryTable secondaryTable = getSecondaryTable(derivedType, secondary);
                                    tableRefs.put(originalInst, getTableName(secondaryTable));
    
                                    registerSecondaryTable(derived, secondaryTable.name(), originalInst);
    
                                    assoc = getAssociation(derivedType, secondaryTable);
                                }
    
                                return renderAssociation(new StringBuilder(), assoc, aliases, originalInst, derived);
                            };
                        }
                        */

                        inst = resolveLabel(aliases, inst);

                        if (!undeclaredAliases.Contains(inst)) {
                            @out.Append(IdentifierPath.current(inst));
                            instMutating = inst;
                        }
                    }
                    else {
                        originalInst = null;
                    }

                    ISequence<char> instFinal = instMutating;

                    if (function != null) {
                        var functionName = function.Name == null
                            ? function.UnderscoresAsBlanks
                                ? GetName(m).Replace(UNDERSCORE_CHAR, KEYWORD_DELIMITER_CHAR)
                                : GetName(m)
                            : function.Name;

                        var @operator = m.GetCustomAttribute<OperatorAttribute>();

                        //return () => {

                            var pp = (IList<ISequence<char>>)boundArgs1.Select(_ => _()).ToList();
                            var originalParams = pp;

                            ISequence<char> currentSubQuery = (cte != null
                                                               && cte.Value == CommonTableExpressionType.Decorator)
                                ? subQueries.sealName(pp.get(0))
                                : null;

                            if (instance != null && @out.Length > 0) {
                                @out.Append(KEYWORD_DELIMITER_CHAR);
                            }

                            var argsBuilderBound = new List<Func<ISequence<char>, ISequence<char>>>();
                            pp = expandVarArgs(pp, argsBuilder, argsBuilderBound);
                            
                            var viewRow = m.GetCustomAttribute<ViewDeclarationAttribute.RowAttribute>();
                            if (viewRow != null) {
                                @out.Length = (0);
                                var view = views.get(originalInst);
                                evaluationContextView = view;

                                if (viewRow.Aliased) {
                                    argsBuilderBound.Clear();

                                    for (int i = 0; i < pp.size(); i++) {
                                        int ip = i;
                                        argsBuilderBound.Add(p => view.getColumn(p, ip));
                                    }
                                }
                            }

                            var viewFrom = m.GetCustomAttribute<ViewDeclarationAttribute.FromAttribute>();
                            if (viewFrom != null) {
                                View view = views.get(originalInst);
                                evaluationContextView = view;

                                Func<int, Func<ISequence<char>, ISequence<char>>> paramBuilder = limit => p => {
                                    if (p != null) {
                                        return (p is ParameterRef @ref)
                                            ? view.getSelect(@ref.Value, limit, viewFrom.Aliased)
                                            : view.getSelect(resolveLabel(aliases, p), limit, viewFrom.Aliased);
                                    }
                                    else
                                        return view.getSelect();
                                };

                                int size = argsBuilderBound.size();
                                if (argsBuilderBound.IsEmpty()) {
                                    argsBuilderBound.Add(paramBuilder(0));
                                    contextAttributes = new ContextAttribute[1];
                                    pp.Add(null);
                                }
                                else
                                    argsBuilderBound.set(0, paramBuilder(1 - size));

                                if (viewFrom.Aliased) {
                                    for (int i = 1; i < size; i++) {
                                        int ip = i - size;
                                        Func<ISequence<char>, ISequence<char>> bound = argsBuilderBound.get(i);
                                        argsBuilderBound.set(i, bound.andThen(p => view.getColumn(p, ip)));
                                    }
                                }

                                @out.Length = (0);
                            }

                            /*if (m.IsDefined(typeof(ViewDeclarationAttribute.AliasAttribute))) {
                                View view = views.get(originalInst);
                                argsBuilderBound.Clear();

                                for (int i = 0; i < pp.size(); i++) {
                                    int ip = i;
                                    argsBuilderBound.Add(p => view.getColumn(p, ip));
                                }

                                @out.Length = (0);
                            }*/

                            var args = pp.Zip(argsBuilderBound, (arg, builder) => builder(arg)).
                                        Zip(contextAttributes, (arg, ca) => {
                                            var format = ca?.Format;
                                            return arg != null && format != null ? String.Format(format, arg) : arg?.ToString();
                                        });

                            String delimiter = function.OmitArgumentsDelimiter
                                ? KEYWORD_DELIMITER
                                : function.ArgumentsDelimiter + KEYWORD_DELIMITER;

                            bool omitParentheses;
                            if (@operator == null) {
                                omitParentheses = function.OmitParentheses
                                                  || (function.OmitParenthesesIfArgumentless && argsBuilderBound.IsEmpty());
                                @out.Append(functionName);
                                @out.Append(omitParentheses ? KEYWORD_DELIMITER : LEFT_PARAN);

                                String collectedArgs = String.Join(delimiter, args);
                                @out.Append(collectedArgs);
                            }
                            else {
                                omitParentheses = function.OmitParentheses;
                                @out.Append(omitParentheses ? KEYWORD_DELIMITER : LEFT_PARAN);

                                var it = args.GetEnumerator();
                                it.MoveNext(); // must have at least one arg
                                var next = it.Current;

                                if (@operator.Right) {
                                    @out.Append(next).Append(KEYWORD_DELIMITER).Append(functionName);
                                }
                                else {
                                    @out.Append(functionName).Append(KEYWORD_DELIMITER).Append(next);
                                }

                                if (it.MoveNext()) {
                                    @out.Append(@operator.OmitParentheses ? KEYWORD_DELIMITER : LEFT_PARAN);

                                    do {
                                        next = it.Current;
                                        @out.Append(next).Append(delimiter);
                                    } while (it.MoveNext());

                                    @out.Length = (@out.Length - delimiter.Length);

                                    @out.Append(@operator.OmitParentheses ? KEYWORD_DELIMITER : RIGHT_PARAN);
                                }
                            }

                            @out.Append(omitParentheses ? KEYWORD_DELIMITER : RIGHT_PARAN);

                            if (IsDefined<NoOpAttribute>(m)) {
                                return null;
                            }

                            if (currentSubQuery != null) // decorator is optional
                                return handleView(subQueries.put(@out.AsSequence(), currentSubQuery), m, originalParams, boundArgs);

                            if (function.RequiresAlias) {
                                StringBuilder implicitAlias = new StringBuilder().Append(TABLE_ALIAS_PREFIX)
                                    .Append(parameterCounter++);
                                if (function.OmitParentheses) {
                                    RequiresParenthesesInAS specialCase = new RequiresParenthesesInAS(@out.AsSequence());
                                    aliases.put(specialCase, implicitAlias.AsSequence());
                                    return specialCase;
                                }

                                aliases.put(@out.AsSequence(), implicitAlias.AsSequence());
                            }

                            return handleView(@out.AsSequence(), m, originalParams, boundArgs);
                       // };
                    }
                    
                    if (m.IsDefined(typeof(NoOpAttribute))) {
                        return null;
                    }

                    if (isSubQuery) {
                        return subQueries.put(localSubQueryAlias, boundArgs1.First()());
                    }

                    //return () => {
                        if (renderingAssociation) {
                            // we cannot resolve other side of the association without this side
                            // so let's handle them together
                            return @out.AsSequence();
                        }

                        if (isEmbeddable(m.ReturnType) || isCollection(m.ReturnType) || isEmbedded(m))
                            // embedded
                            return calcOverrides(@out.AsSequence(), m, tableSecondaryRefs.get(@out.AsSequence()));

                        if (m.MetadataToken == HAS_VALUE)
                            return @out.Append(KEYWORD_DELIMITER).Append(IS_NOT_NULL).AsSequence();

                        IdentifierPath columnName = getColumnNameFromProperty(m, ei?.Type);

                        if (m.GetParameters().Length > 0) // assignment
                            return new StringBuilder().Append(
                                    columnName.resolveInstance(instFinal, true, tableSecondaryRefs.get(instFinal)))
                                .Append(KEYWORD_DELIMITER + EQUAL_SIGN + KEYWORD_DELIMITER)
                                .Append(boundArgs1.First()()).AsSequence();

                        if (instFinal != null) {
                            return columnName.resolveInstance(instFinal, tableSecondaryRefs.get(instFinal));
                        }

                        @out.Append(columnName);

                        return @out.AsSequence();
  //                  };
                };
            };
        }

        private ISequence<char> handleView(ISequence<char> result,
            MethodInfo m,
            IList<ISequence<char>> originalParams,
            IList<Expression> arguments) {
            if (m.IsDefined(typeof(ViewDeclarationAttribute))) {
                if (views.IsEmpty())
                    views = new Dictionary<ISequence<char>, View>();

                var tablePart = originalParams[0].Length + 1;
                views.put(result, new View(originalParams.GetRange(1).
                    Select(col => col.SubSequence(tablePart, col.Length).ToString()).ToList(), arguments, this));
            }

            return result;
        }

        // terminal function
        private Func<ISequence<char>, ISequence<char>> tableReference(Expression e,
            SubQueryManager subQueries,
            IDictionary<ISequence<char>, ISequence<char>> aliases) {
            return seq => { return handleFromClause(seq, e, aliases, subQueries); };
        }

        private ISequence<char> resolveTableName(ISequence<char> seq,
            Type resultType) {
            /*Member joinTable = joinTablesForFROM.get(seq);
            if (joinTable != null)
                return getJoinTableName(joinTable);

            joinTable = collectionTablesForFROM.get(seq);
            if (joinTable != null)
                return getECTableName(joinTable);*/

            return getTableName(resultType).AsSequence();
        }

        private ISequence<char> handleFromClause(ISequence<char> seq,
            Expression e,
            IDictionary<ISequence<char>, ISequence<char>> aliases,
            SubQueryManager subQueries) {
            var resultType = e.getResultType();
            if (!isCollection(resultType) && !isScalar(resultType) && (typeof(Object) != resultType)
                && !isEntityLike(resultType))
                throw TranslationError.INVALID_FROM_PARAM.getError(resultType);

            ISequence<char> label = aliases.get(seq);
            bool hasLabel = label != null;
            if (!hasLabel)
                label = seq;

            if (SubQueryManager.isSubQuery(seq)) {
                ISequence<char> name = SubQueryManager.getName(seq);
                if (name != seq) {
                    if (!hasLabel)
                        label = name;
                    StringBuilder fromBuilder = SubQueryManager.isRequiresParentheses(seq)
                        ? new StringBuilder().Append(LEFT_PARAN).Append(seq).Append(RIGHT_PARAN)
                        : new StringBuilder().Append(seq);
                    fromBuilder.Append(ExLINQ.Capabilities.Contains(Capability.TABLE_AS_ALIAS) ? SEP_AS_SEP : KEYWORD_DELIMITER);
                    var aliased = fromBuilder.Append(label);
                    
                    if (tupleViewMap.TryGetValue(seq, out var view))
                        aliased.Append(KEYWORD_DELIMITER + LEFT_PARAN).Append(view.getSelect()).Append(RIGHT_PARAN);

                    return aliased.AsSequence();
                }

                return label;
            }

            String refName = tableRefs.get(seq);
            ISequence<char> tableName = refName != null
                ? Object.ReferenceEquals(refName, PRIMARY) ? resolveTableName(seq, resultType) : refName.AsSequence()
                : null;
            if (hasLabel && tableName == null)
                tableName = seq is RequiresParenthesesInAS @as
                    ? new StringBuilder().Append(LEFT_PARAN).Append(@as.Wrapped)
                        .Append(RIGHT_PARAN).AsSequence()
                    : seq;

            if (tableName != null) {
                if (renderingContext == ParameterContext.From
                    || (renderingContext == ParameterContext.FromWithoutAlias && hasLabel)) {
                    // undeclaredAliases.Remove(label); //TODO ??
                    StringBuilder fromBuilder = new StringBuilder().Append(tableName);
                    fromBuilder.Append(ExLINQ.Capabilities.Contains(Capability.TABLE_AS_ALIAS) ? SEP_AS_SEP : KEYWORD_DELIMITER);
                    var aliased = fromBuilder.Append(label);
                    
                    if (tupleViewMap.TryGetValue(seq, out var view))
                        aliased.Append(KEYWORD_DELIMITER + LEFT_PARAN).Append(view.getSelect()).Append(RIGHT_PARAN);
                    
                    return aliased.AsSequence();
                }
                else {
                    addUndeclaredAlias(label);
                    return tableName;
                }
            }

            return seq;
        }

        // terminal function
        private Func<ISequence<char>, ISequence<char>> expression(Expression e,
            SubQueryManager subQueries,
            IDictionary<ISequence<char>, ISequence<char>> aliases) {
            bool isEntity = isEntityLike(e.getResultType());

            return seq => {
                if (renderingContext == ParameterContext.Alias)
                    return extractColumnName(seq);

                if (e is ParameterExpression || (e is MemberExpression me && me.Expression.Type.IsSynthetic())) {
                    if (isEntity) {
                        switch (renderingContext) {
                            case ParameterContext.Select:
                                if (IdentifierPath.isResolved(seq))
                                    break;
                                seq = resolveLabel(aliases, seq);
                                return new StringBuilder().Append(seq).Append(DOT + STAR).AsSequence();

                            case ParameterContext.From:
                            case ParameterContext.FromWithoutAlias:
                                return handleFromClause(seq, e, aliases, subQueries);
                        }
                    }
                }

                if (renderingContext == ParameterContext.Select) {
                    ISequence<char> label = aliases.get(seq);

                    if (label != null) {
                        return new StringBuilder().Append(verifyParentheses(seq)).Append(SEP_AS_SEP).Append(label).AsSequence();
                    }
                }

                return verifyParentheses(seq);
            };
        }

        protected override Expression VisitNew(NewExpression node) {
            var x = node.Type.IsValueType;
            if (x) {
                var boundArgs = node.Arguments.Select(Visit).Select(_ => _.As<Func<Func<ISequence<char>>>>()).ToList();
                Func<Func<ISequence<char>>> result = () => {

                    var boundArgs1 = boundArgs.Select(_ => _()).ToList();

                    return () => {
                        var expr = String.Join(COMMA, boundArgs1.Select(_ => _()));
                        if (typeof(ITuple).IsAssignableFrom(node.Type))
                            expr = (LEFT_PARAN + expr + RIGHT_PARAN); 
                        return expr.AsSequence();
                    };
                };

                return result.AsXExpression();
            }
            return base.VisitNew(node); 
        }

        protected override Expression VisitParameter(ParameterExpression node) {
            return visit(node).AsXExpression();
        }

        public Func<Func<ISequence<char>>> visit(ParameterExpression e) {
            return () => () => {
                    // int index = e.getIndex();
                    //
                    // if (t.IsEmpty() || index >= t.size()) {
                    if (parameterResults.TryGetValue(e, out var seq))
                        return seq;
                    
                        var tableRef = CalcTableReference(e, PRIMARY);
                        return parameterResults[e] = tableRef;
            };
        }

        private ISequence<char> CalcTableReference(Expression e, String table) {
            var resultType = e.getResultType();
            if (resultType.IsDefined(typeof(NoOpAttribute)))
                return null;
            if (!isEntityLike(resultType))
                throw TranslationError.CANNOT_CALCULATE_TABLE_REFERENCE.getError(resultType);
            ISequence<char> tableRef = calcOverrides(new StringBuilder().Append(TABLE_ALIAS_PREFIX).Append(parameterCounter++).AsSequence(), resultType, null);
            tableRefs.put(tableRef, table);
            return tableRef;
        }

        protected override Expression VisitUnary(UnaryExpression node) {
            return visit(node).AsXExpression();
        }

        public Func<Func<ISequence<char>>> visit(UnaryExpression e) {
            if (e.getExpressionType() == ExpressionType.Not && e.Operand is MethodCallExpression mce && mce.Method.MetadataToken == HAS_VALUE) {
                var fobj = Visit(mce.Object).As<Func<Func<ISequence<char>>>>();
                return () => {
                    var obj = fobj();
                    return () => UnaryOperator.IsNull(obj());
                };
            }

            var ffirst = Visit(e.Operand).As<Func<Func<ISequence<char>>>>();

            return () => {
                var first = ffirst();
                switch (e.getExpressionType()) {
                    /*case ExpressionType.IsNull:
                        return (args) -> UnaryOperator.IsNull.eval(first.apply(args));
                    case ExpressionType.IsNonNull:
                        return (args) -> UnaryOperator.IsNonNull.eval(first.apply(args));*/
                    case ExpressionType.Convert:
                        return () => first();
                    case ExpressionType.Not:

                        if (e.Type == typeof(bool))
                            return () => UnaryOperator.LogicalNot(first());

                        return () => UnaryOperator.BitwiseNot(first());

                    case ExpressionType.Negate:
                        return () => UnaryOperator.Negate(first());
                    default:
                        throw
                            TranslationError.UNSUPPORTED_EXPRESSION_TYPE.getError(getOperatorSign(e.getExpressionType()));
                }
            };
        }

        protected override Expression VisitBlock(BlockExpression node) {
            return visit(node).AsXExpression();
        }

        public Func<Func<ISequence<char>>> visit(BlockExpression e) {
            var expressions = (IEnumerable<Expression>)e.Expressions;

            var fexprs = expressions
                .Select(p => Visit(p).As<Func<Func<ISequence<char>>>>()).ToList();

            return () => {

                var ppe = fexprs.Select(p => p()).ToList();

                return () => {

                    StringBuilder combined = null;
                    ISequence<char> lastResult = null;
                    foreach (var pe in ppe) {
                        ParameterContext previousRenderingContext = this.renderingContext;
                        renderingContext = ParameterContext.Expression;
                        try {
                            var x = pe();
                            if (x == null)
                                continue;

                            if (lastResult == null) {
                                lastResult = x;
                                continue;
                            }

                            x = Strings.trim(x);
                            if (Strings.isNullOrEmpty(x))
                                continue;

                            if (combined != null) {
                                combined.AppendLine().Append(x);
                                continue;
                            }

                            combined = new StringBuilder();
                            lastResult = Strings.trim(lastResult);
                            if (!Strings.isNullOrEmpty(lastResult))
                                combined.AppendLine(lastResult.ToString());

                            combined.Append(x);
                        }
                        finally {
                            renderingContext = previousRenderingContext;
                        }
                    }

                    return combined != null ? combined.AsSequence() : lastResult;
                };
            };
        }

        protected override Expression VisitNewArray(NewArrayExpression node) {
            return visit(node).AsXExpression();
        }

        public Func<Func<ISequence<char>>> visit(NewArrayExpression e) {
            var allArgs = e.Expressions;
            var fexprs = allArgs
                .Select(p => Visit(p).As<Func<Func<ISequence<char>>>>());

            return () => {
                var ppe = fexprs.Select(p => p()).ToList();

                var curArgs = bind(allArgs);

                return () => new PackedInitializers(curArgs, ppe, null, this);
            };
        }
    }
}