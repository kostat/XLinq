using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Streamx.Linq.ExTree {
    internal sealed partial class MethodVisitor {
        private static readonly Type[] INTEGRAL_TYPES = {typeof(byte), typeof(short), typeof(int), typeof(long)};
        private static readonly Type[] FLOATING_TYPES = {typeof(float), typeof(double)};

        private ExpressionStack _exprStack;
        private readonly List<Expression> _statements = new List<Expression>();
        private readonly List<ParameterExpression> _variables = new List<ParameterExpression>();
        private int _customVarIndex;

        private readonly Dictionary<Label, List<ExpressionStack>> _branches =
            new Dictionary<Label, List<ExpressionStack>>();

        private readonly Expression _target;
        private readonly IList<ParameterExpression> _params;
        private readonly IList<Expression> _arguments;
        private readonly Type _returnType;

        // ReSharper disable once PossibleNullReferenceException
        internal static readonly int HAS_VALUE = typeof(Nullable<>).GetProperty("HasValue").GetMethod.MetadataToken;

        // ReSharper disable once PossibleNullReferenceException
        private static readonly int ARRAY_EMPTY = typeof(Array).GetMethod("Empty").MetadataToken;

        public MethodVisitor(Expression target, IList<ParameterExpression> @params, Type returnType, IList<Expression> arguments) {
            _target = target;
            _params = @params;
            _returnType = returnType;

            _arguments = arguments?.Count > 0 ? arguments.Select(_ => _.HasParameters() ? null : _).ToList() : null;
        }

        public ReadOnlyCollection<Expression> Statements => _statements.AsReadOnly();

        public ReadOnlyCollection<ParameterExpression> Variables => _variables.AsReadOnly();

        public bool NotCacheable { get; set; }

        private List<ExpressionStack> GetBranchUsers(Label label) {
            if (!_branches.TryGetValue(label, out var bl)) {
                bl = new List<ExpressionStack>();
                _branches[label] = bl;
            }

            return bl;
        }

        private void Branch(Label label,
            Expression test) {
            List<ExpressionStack> bl = GetBranchUsers(label);

            ExpressionStack.BranchExpression br = new ExpressionStack.BranchExpression(_exprStack, test);
            _exprStack.Push(br);

            ExpressionStack left = br.False;
            bl.Add(left);
            _exprStack = br.True;
        }

        private void Go(Label label) {
            GetBranchUsers(label).Add(_exprStack);

            _exprStack = null;
        }

        public void VisitCode() {
            _exprStack = new ExpressionStack();
        }

        public void VisitEnd() {
            VisitLabel(Label.Finish);

            if (_exprStack.Any()) {
                _statements.AddRange(_exprStack);
            }
            else {
                Debug.Assert(_returnType == typeof(void));
            }

            if (_statements.Any()) {
                var statementsLastIndex = _statements.Count - 1;
                var last = _statements[statementsLastIndex];
                _exprStack.Sort(_statements);
                var newLast = _statements[statementsLastIndex];
                if (last != newLast) {
                    var lastIndex = _statements.LastIndexOf(last);
                    if (last is ParameterExpression var) {
                        // simple return statement reordering
                        _statements.RemoveAt(lastIndex);
                        _statements.Add(var);
                    }
                    else if (!last.IsVoid()) {
                        var = Expression.Variable(last.Type, LocalVariable.VARIABLE_PREFIX + _exprStack.LocalVariables.Length);
                        _variables.Add(var);
                        _statements[lastIndex] = Expression.Assign(var, last);
                        statementsLastIndex++;
                        _statements.Add(var);
                    }
                }

                if (_returnType != typeof(void)) {

                    for (var i = statementsLastIndex; i >= 0; i--) {
                        last = _statements[i];
                        if (!last.IsVoid()) {
                            newLast = TypeConverter.Convert(last, _returnType);
                            if (newLast != last)
                                _statements[i] = newLast;
                            if (i < statementsLastIndex) {
                                // last statement(s) can return void
                                // from evaluation perspective the execution stack remains unchanged, but Block() fails
                                var var = Expression.Variable(newLast.Type, LocalVariable.VARIABLE_PREFIX + _exprStack.LocalVariables.Length);
                                _variables.Add(var);
                                _statements[i] = Expression.Assign(var, newLast);
                                _statements.Add(var);
                            }

                            break;
                        }
                    }
                }
            }
        }

        public void VisitFieldInsn(ILOpCode opCode, FieldInfo fieldInfo) {
            Expression e;
            switch (opCode) {
                case ILOpCode.Ldfld:
                case ILOpCode.Ldflda:
                    Expression instance = _exprStack.Pop();
                    e = Expression.Field(instance, fieldInfo);

                    break;
                case ILOpCode.Ldsfld:
                case ILOpCode.Ldsflda:
                    e = fieldInfo.DeclaringType.IsSynthetic() || fieldInfo.IsSynthetic()
                        ? CreateDefaultConstant(fieldInfo.FieldType)
                        : Expression.Field(null, fieldInfo);
                    break;
                case ILOpCode.Stsfld:
                    if (fieldInfo.DeclaringType.IsSynthetic() || fieldInfo.IsSynthetic()) {
                        _exprStack.Pop();
                        return;
                    }

                    goto default;
                case ILOpCode.Stfld:
                    if (fieldInfo.DeclaringType.IsSynthetic() || fieldInfo.IsSynthetic()) {
                        e = _exprStack.Pop();
                        var var = _exprStack.Pop();

                        if (var.NodeType == ExpressionType.MemberAccess)
                            return;

                        if (var.NodeType != ExpressionType.Parameter && var.NodeType != ExpressionType.Constant)
                            goto default;
                        e = Expression.Assign(Expression.Field(var, fieldInfo), e);
                        _statements.Add(e);
                        _exprStack.TrackOrder(e);
                        return;
                    }

                    goto default;
                default:
                    throw NotLambda(opCode);
            }

            _exprStack.Push(e);
        }

        private static Expression CreateDefaultConstant(Type type) =>
            Expression.Constant(type.IsValueType ? Activator.CreateInstance(type) : null, type);

        public void VisitInsn(ILOpCode opCode) {
            Expression e;
            Expression first;
            Expression second;

            switch (opCode) {
                case ILOpCode.Add:
                case ILOpCode.Add_ovf:
                case ILOpCode.Add_ovf_un:
                    first = _exprStack.Pop();
                    second = _exprStack.Pop();
                    e = Expressions.Add(second, first);
                    break;
                case ILOpCode.And:
                    first = _exprStack.Pop();
                    second = _exprStack.Pop();
                    e = first.IsInt31() ? second : first.IsBool() && second.IsBool() ? Expression.AndAlso(second, first) : Expression.And(second, first);
                    break;
                case ILOpCode.Ceq:
                    first = _exprStack.Pop();
                    second = _exprStack.Pop();

                    e = Expressions.Equal(second, first);
                    break;
                case ILOpCode.Cgt:
                    first = _exprStack.Pop();
                    second = _exprStack.Pop();
                    e = Expressions.CreateNumericComparison(ExpressionType.GreaterThan, second.EnsureNumeric(), first.EnsureNumeric());
                    break;
                case ILOpCode.Cgt_un:
                    first = _exprStack.Pop();
                    second = _exprStack.Pop();
                    // special case
                    if (first is ConstantExpression firstNull && firstNull.Value == null)
                        e = second.Type == typeof(bool) ? second : Expressions.NotEqual(second, first);
                    else
                        e = Expressions.CreateNumericComparison(ExpressionType.GreaterThan, second.EnsureNumeric(), first.EnsureNumeric());
                    break;
                case ILOpCode.Clt:
                case ILOpCode.Clt_un:
                    first  = _exprStack.Pop();
                    second = _exprStack.Pop();
                    e = Expressions.CreateNumericComparison(ExpressionType.LessThan, second.EnsureNumeric(), first.EnsureNumeric());
                    break;
                case ILOpCode.Conv_i1:
                case ILOpCode.Conv_i2:
                case ILOpCode.Conv_i4:
                case ILOpCode.Conv_i8:
                    first = _exprStack.Pop();
                    e = Expression.Convert(first, INTEGRAL_TYPES[opCode - ILOpCode.Conv_i1]);
                    break;
                case ILOpCode.Conv_ovf_i1:
                case ILOpCode.Conv_ovf_i2:
                case ILOpCode.Conv_ovf_i4:
                case ILOpCode.Conv_ovf_i8:
                    first = _exprStack.Pop();
                    e = Expression.Convert(first, INTEGRAL_TYPES[(opCode - ILOpCode.Conv_ovf_i1) >> 1]);
                    break;
                case ILOpCode.Conv_ovf_i1_un:
                case ILOpCode.Conv_ovf_i2_un:
                case ILOpCode.Conv_ovf_i4_un:
                case ILOpCode.Conv_ovf_i8_un:
                    first = _exprStack.Pop();
                    e = Expression.Convert(first, INTEGRAL_TYPES[opCode - ILOpCode.Conv_ovf_i1_un]);
                    break;
                case ILOpCode.Conv_ovf_u1:
                case ILOpCode.Conv_ovf_u2:
                case ILOpCode.Conv_ovf_u4:
                case ILOpCode.Conv_ovf_u8:
                    first = _exprStack.Pop();
                    e = Expression.Convert(first, INTEGRAL_TYPES[(opCode - ILOpCode.Conv_ovf_u1) >> 1]);
                    break;
                case ILOpCode.Conv_ovf_u1_un:
                case ILOpCode.Conv_ovf_u2_un:
                case ILOpCode.Conv_ovf_u4_un:
                case ILOpCode.Conv_ovf_u8_un:
                    first = _exprStack.Pop();
                    e = Expression.Convert(first, INTEGRAL_TYPES[opCode - ILOpCode.Conv_ovf_u1_un]);
                    break;
                case ILOpCode.Conv_r_un:
                    first = _exprStack.Pop();
                    e = Expression.Convert(first, typeof(float));
                    break;
                case ILOpCode.Conv_r4:
                case ILOpCode.Conv_r8:
                    first = _exprStack.Pop();
                    e = Expression.Convert(first, FLOATING_TYPES[opCode - ILOpCode.Conv_r4]);
                    break;
                case ILOpCode.Conv_u1:
                    first = _exprStack.Pop();
                    e = Expression.Convert(first, INTEGRAL_TYPES[0]);
                    break;
                case ILOpCode.Conv_u2:
                    first = _exprStack.Pop();
                    e = Expression.Convert(first, INTEGRAL_TYPES[1]);
                    break;
                case ILOpCode.Conv_u4:
                case ILOpCode.Conv_u8:
                    first = _exprStack.Pop();
                    e = Expression.Convert(first, INTEGRAL_TYPES[opCode - ILOpCode.Conv_u4 + 2]);
                    break;
                case ILOpCode.Div:
                case ILOpCode.Div_un:
                    first = _exprStack.Pop();
                    second = _exprStack.Pop();
                    e = Expressions.Divide(second, first);
                    break;
                case ILOpCode.Dup:
                    e = _exprStack.Peek();
                    if (e.HasCalls()) {
                        e = CreateVariableForExpression(_exprStack.Pop());
                        _exprStack.Push(e);
                    }

                    break;
                case ILOpCode.Ldelem:
                case ILOpCode.Ldelem_i1:
                case ILOpCode.Ldelem_i2:
                case ILOpCode.Ldelem_i4:
                case ILOpCode.Ldelem_i8:
                case ILOpCode.Ldelem_r4:
                case ILOpCode.Ldelem_r8:
                case ILOpCode.Ldelem_ref:
                case ILOpCode.Ldelem_u1:
                case ILOpCode.Ldelem_u2:
                case ILOpCode.Ldelem_u4:
                    first = _exprStack.Pop();
                    second = _exprStack.Pop();
                    e = Expression.ArrayIndex(second, first);
                    break;
                case ILOpCode.Ldlen:
                    first = _exprStack.Pop();
                    e = Expression.ArrayLength(first);
                    break;
                case ILOpCode.Mul:
                case ILOpCode.Mul_ovf:
                case ILOpCode.Mul_ovf_un:
                    first = _exprStack.Pop();
                    second = _exprStack.Pop();
                    e = Expressions.Multiply(second, first);
                    break;
                case ILOpCode.Neg:
                    first = _exprStack.Pop();
                    e = Expression.Negate(first);
                    break;
                case ILOpCode.Not:
                    first = _exprStack.Pop();
                    e = first.IsBool() ? Expressions.LogicalNot(first) : Expression.Not(first);
                    break;
                case ILOpCode.Or:
                    first = _exprStack.Pop();
                    second = _exprStack.Pop();
                    e = Expression.Or(second, first);
                    break;
                case ILOpCode.Pop:
                    e = _exprStack.Pop();

                    Predicate<Expression> isInteresting = x => x != null && !(x is ConstantExpression || x is ParameterExpression || x is MemberExpression);

                    if (isInteresting(e)) {
                        for (var i = 0; i < _exprStack.Count; i++) {
                            var ee = _exprStack[i];
                            if (!isInteresting(ee))
                                continue;

                            _exprStack[i] = CreateVariableForExpression(ee);
                        }

                        _statements.Add(e);
                    }

                    return;
                case ILOpCode.Rem:
                case ILOpCode.Rem_un:
                    first = _exprStack.Pop();
                    second = _exprStack.Pop();
                    e = Expressions.Modulo(second, first);
                    break;
                case ILOpCode.Ret:
                    Go(Label.Finish);
                    return;
                case ILOpCode.Shl:
                    first = _exprStack.Pop();
                    second = _exprStack.Pop();
                    e = Expression.LeftShift(second, first);
                    break;
                case ILOpCode.Shr:
                case ILOpCode.Shr_un:
                    first = _exprStack.Pop();
                    second = _exprStack.Pop();
                    e = Expression.RightShift(second, first);
                    break;
                case ILOpCode.Stelem:
                case ILOpCode.Stelem_i1:
                case ILOpCode.Stelem_i2:
                case ILOpCode.Stelem_i4:
                case ILOpCode.Stelem_i8:
                case ILOpCode.Stelem_r4:
                case ILOpCode.Stelem_r8:
                case ILOpCode.Stelem_ref:
                    var value = _exprStack.Pop();
                    var index = _exprStack.Pop() as ConstantExpression;
                    var newArrayInit = _exprStack.Pop() as NewArrayExpression;
                    if (index == null || index.Type != typeof(int))
                        throw NotLambda(opCode);

                    if (newArrayInit == null || newArrayInit != _exprStack.Pop())
                        throw NotLambda(opCode);

                    var expressions = newArrayInit.Expressions.ToArray();
                    expressions[(int) index.Value] = TypeConverter.Convert(value, newArrayInit.Type.GetElementType());
                    e = newArrayInit.Update(expressions);
                    break;
                case ILOpCode.Sub:
                case ILOpCode.Sub_ovf:
                case ILOpCode.Sub_ovf_un:
                    first = _exprStack.Pop();
                    second = _exprStack.Pop();
                    e = Expressions.Subtract(second, first);
                    break;
                case ILOpCode.Xor:
                    first = _exprStack.Pop();
                    second = _exprStack.Pop();
                    e = Expression.ExclusiveOr(second, first);
                    break;
                default:
                    throw NotLambda(opCode);
            }

            _exprStack.Push(e);
        }

        private ParameterExpression CreateVariableForExpression(Expression ee) {
            var var = Expression.Variable(ee.Type, LocalVariable.VARIABLE_PREFIX + _customVarIndex++);
            _variables.Add(var);
            var assign = Expression.Assign(var, ee);
            _statements.Add(assign);
            _exprStack.TrackOrder(assign, ee);
            _exprStack.TrackOrder(var);
            return var;
        }

        public bool VisitJumpInsn(ILOpCode opCode, Label label) {
            ExpressionType eType;
            Expression e;
            switch (opCode) {
                case ILOpCode.Beq:
                case ILOpCode.Beq_s:
                    eType = ExpressionType.NotEqual; // Equal
                    break;
                case ILOpCode.Bge:
                case ILOpCode.Bge_s:
                case ILOpCode.Bge_un:
                case ILOpCode.Bge_un_s:
                    eType = ExpressionType.LessThan; // GreaterThanOrEqual
                    break;
                case ILOpCode.Bgt:
                case ILOpCode.Bgt_un:
                case ILOpCode.Bgt_s:
                case ILOpCode.Bgt_un_s:
                    eType = ExpressionType.LessThanOrEqual; // GreaterThan
                    break;
                case ILOpCode.Ble:
                case ILOpCode.Ble_un:
                case ILOpCode.Ble_s:
                case ILOpCode.Ble_un_s:
                    eType = ExpressionType.GreaterThan; // LessThanOrEqual
                    break;
                case ILOpCode.Blt:
                case ILOpCode.Blt_un:
                case ILOpCode.Blt_s:
                case ILOpCode.Blt_un_s:
                    eType = ExpressionType.GreaterThanOrEqual; // LessThan
                    break;
                case ILOpCode.Bne_un:
                case ILOpCode.Bne_un_s:
                    eType = ExpressionType.Equal; // NotEqual
                    break;
                case ILOpCode.Br:
                case ILOpCode.Br_s:
                    Go(label);

                    return false;
                case ILOpCode.Brfalse:
                case ILOpCode.Brfalse_s:
                    e = _exprStack.Pop();
                    if (e.IsConstBoolLike(out bool value1)) {
                        if (!value1)
                            return true;
                    }
                    else {
                        Expression isnull = Expressions.IsTrue(e); // inverse
                        Branch(label, isnull);
                    }

                    return false;

                case ILOpCode.Brtrue:
                case ILOpCode.Brtrue_s:
                    e = _exprStack.Pop();

                    if (e.IsConstBoolLike(out bool value)) {
                        if (value)
                            return true;
                    }
                    else {
                        if (typeof(Delegate).IsAssignableFrom(e.Type))
                            return false;
                        if (!e.Type.IsPrimitive)
                            return true;
                        if (e is MethodCallExpression mce && mce.Method.MetadataToken == HAS_VALUE)
                            return true;
                        Expression isNonNull = Expressions.IsFalse(e); // inverse
                        Branch(label, isNonNull);
                    }

                    return false;

                default:
                    throw NotLambda(opCode);
            }

            Expression second = _exprStack.Pop();
            Expression first = _exprStack.Pop();
            if (eType != ExpressionType.Equal)
                first = first.EnsureNumeric();
            e = Expression.MakeBinary(eType, first, TypeConverter.Convert(second, first.Type));

            Branch(label, e);

            return false;
        }

        private ExpressionStack Reduce(ExpressionStack first,
            ExpressionStack second) {
            int fDepth = first.Depth;
            int sDepth = second.Depth;

            if (fDepth == sDepth) {
                ExpressionStack.BranchExpression firstB = first.Parent;
                ExpressionStack.BranchExpression secondB = second.Parent;

                if (firstB == secondB) {
                    ExpressionStack parentStack = firstB.Parent;
                    parentStack.Pop(); // branch

                    Expression right = firstB.True.Pop();
                    Expression left = firstB.False.Pop();

                    if (right != null || left != null)
                        parentStack.Push(Expressions.Condition(firstB.Test, right, left));

                    for (int i = 0; i < (parentStack.LocalVariables?.Length ?? 0); i++) {
                        right = firstB.True.LocalVariables[i].Get(_statements, _variables);
                        left = firstB.False.LocalVariables[i].Get(_statements, _variables);

                        if (right != left && left != null) {

                            var condition = Expressions.Condition(firstB.Test, right, left);

                            // ReSharper disable once PossibleNullReferenceException
                            parentStack.LocalVariables[i].Assign(condition, parentStack);
                        }
                    }

                    return parentStack;
                }

                if (first.Count == 0 && second.Count == 0) {
                    ExpressionStack.BranchExpression firstBB = firstB.Parent.Parent;
                    ExpressionStack.BranchExpression secondBB = secondB.Parent.Parent;

                    if (firstBB == secondBB) {
                        ExpressionStack l;

                        Expression fTest = firstB.Test;
                        if (firstB.True != first) {
                            fTest = Expressions.LogicalNot(fTest);
                            l = firstB.True;
                        }
                        else
                            l = firstB.False;

                        Expression sTest = secondB.Test;
                        if (secondB.True != second) {
                            sTest = Expressions.LogicalNot(sTest);
                            secondB.True.Reduce();
                        }
                        else
                            secondB.False.Reduce();

                        Expression rootTest = firstBB.Test;
                        if (firstBB.True != firstB.Parent)
                            rootTest = Expressions.LogicalNot(rootTest);

                        rootTest = Expressions.Condition(rootTest, fTest, sTest);

                        ExpressionStack parentStack = firstBB.Parent;

                        ExpressionStack.BranchExpression be = new ExpressionStack.BranchExpression(parentStack,
                            rootTest,
                            first,
                            l);

                        parentStack.Pop(); // old branch

                        parentStack.Add(be);

                        return first;
                    }
                }
            }
            else if (first.Count == 0 && second.Count == 0) {
                ExpressionStack older;
                ExpressionStack younger;

                if (fDepth > sDepth) {
                    older = second;
                    younger = first;
                }
                else {
                    older = first;
                    younger = second;
                }

                bool trueB = older.Parent.True == older;

                var youngerBranch = younger.Parent;
                Expression youngTest = youngerBranch.Test;

                ExpressionStack other;
                if (younger.Parent.Get(trueB) != younger) {
                    youngTest = Expressions.LogicalNot(youngTest);
                    other = youngerBranch.Get(trueB);
                }
                else
                    other = youngerBranch.Get(!trueB);

                Expression test = Expressions.LogicalAnd(older.Parent.Test, youngTest);

                if (!trueB)
                    test = Expressions.LogicalNot(test);

                ExpressionStack parentStack = older.Parent.Parent;

                ExpressionStack.BranchExpression be =
                    new ExpressionStack.BranchExpression(parentStack, test, older, other);

                parentStack.Pop(); // old branch

                parentStack.Add(be);

                return older;
            }

            return null;
        }

        private ExpressionStack Reduce(List<ExpressionStack> bl) {
            int index = bl.Count - 1;
            ExpressionStack second = bl[index];
            bl.RemoveAt(index--);
            if (index < 0)
                return second;

            ExpressionStack first = bl[index];
            ExpressionStack reduced = Reduce(first, second);
            if (reduced != null) {
                bl[index] = reduced;
                return Reduce(bl);
            }

            first = Reduce(bl);

            return Reduce(first, second);
        }


        public void VisitLabel(Label label) {
            if (!_branches.TryGetValue(label, out var bl))
                return;

            _branches.Remove(label);

            for (int i = bl.Count - 1; i >= 0; i--) {
                ExpressionStack es = bl[i];
                if (es.IsReduced)
                    bl.RemoveAt(i);
            }

            if (_exprStack != null)
                bl.Add(_exprStack);

            _exprStack = Reduce(bl);
            Debug.Assert(_exprStack != null);
        }

        public void VisitLdcInsn(object cst) {
            _exprStack.Push(Expression.Constant(cst));
        }

        public void VisitMaxs(int maxStack, IList<LocalVariableInfo> locals) {
            if ((_customVarIndex = locals.Count) > 0) {
                _exprStack.LocalVariables = new LocalVariable[locals.Count];
                for (int i = 0; i < locals.Count; i++) {
                    _exprStack.LocalVariables[i].Info = locals[i];
                }
            }
            else {
                _exprStack.LocalVariables = Array.Empty<LocalVariable>();
            }
        }

        public void VisitMethodInsn(ILOpCode opCode, MethodBase methodBase) {
            if (opCode == ILOpCode.Ldftn) {
                _exprStack.Push(Expression.Constant(methodBase));
                return;
            }

            var arguments = CreateArguments(methodBase.GetParameters());
            Expression e;

            switch (opCode) {
                case ILOpCode.Newobj:

                    var ctor = (ConstructorInfo) methodBase;

                    if (typeof(Delegate).IsAssignableFrom(ctor.DeclaringType)) {
                        if (arguments.Length != 2)
                            throw new InvalidOperationException($"Wrong number of arguments for a delegate: {arguments.Length}");

                        var target = arguments[0];
                        var lambda = (MethodInfo) ((ConstantExpression) arguments[1]).Value;

                        e = ExpressionTree.Parse(ctor.DeclaringType, target, lambda);
                        break;
                    }

                    if (arguments.Length == 0) {
                        e = Expression.Constant(ctor.Invoke(null));
                        break;
                    }

                    e = Expression.New(ctor, arguments);

                    break;
                case ILOpCode.Call:
                case ILOpCode.Calli:
                case ILOpCode.Callvirt:
                    var instance = methodBase.IsStatic ? null : _exprStack.Pop();

                    if (instance != null && typeof(Delegate).IsAssignableFrom(instance) && methodBase.Name.EndsWith("Invoke", StringComparison.Ordinal)) {

                        Delegate compiled;
                        try {
                            compiled = Expression.Lambda(instance).Compile();
                            this.NotCacheable = true;
                        }
                        catch (InvalidOperationException ioe) {
                            // cannot compile
                            goto PERFORM_CALL;
                        }

                        var @delegate = (Delegate) compiled.DynamicInvoke();
                        if (@delegate == null)
                            throw new ArgumentException("Null delegates are illegal. Consider using a delegate returning a constant.");
                        var lambda = ExpressionTree.Parse(@delegate.GetType(), Expression.Constant(@delegate.Target), @delegate.Method);
                        e = Expression.Invoke(lambda, arguments);
                        break;
                    }

                    PERFORM_CALL:
                    e = Expression.Call(instance, (MethodInfo) methodBase, arguments);
                    break;

                default:
                    throw NotLambda(opCode);
            }

            if (e.IsVoid()) {
                _exprStack.TrackOrder(e);
                _statements.Add(e);
            }
            else
                _exprStack.Push(e);
        }

        private Expression[] CreateArguments(ParameterInfo[] @params) {
            var count = @params.Length;
            Expression[] arguments = new Expression[count];
            for (int i = count; i > 0;) {
                i--;
                var param = @params[i];
                var isVararg = param.IsDefined(typeof(ParamArrayAttribute));
                var expression = _exprStack.Pop();
                if (isVararg && expression is MethodCallExpression mce && mce.Method.MetadataToken == ARRAY_EMPTY) {
                    // ReSharper disable once AssignNullToNotNullAttribute
                    expression = Expression.NewArrayInit(expression.Type.GetElementType(), Array.Empty<Expression>());
                }

                arguments[i] = TypeConverter.Convert(expression, Normalize(@params[i].ParameterType));
            }

            return arguments;
        }

        private static Type Normalize(Type type) {
            if (type == typeof(IntPtr))
                return typeof(MethodInfo);

            return type;
        }

        public void VisitTypeInsn(ILOpCode opCode, Type resultType) {
            Expression e;
            switch (opCode) {
                case ILOpCode.Box:
                    e = _exprStack.Pop();
                    if (e.Type.IsValueType)
                        e = Expression.Convert(e, typeof(object));
                    break;
                case ILOpCode.Castclass:
                case ILOpCode.Unbox:
                case ILOpCode.Constrained:
                    e = Expression.Convert(_exprStack.Pop(), resultType);
                    break;
                case ILOpCode.Unbox_any:
                    e = Expression.Convert(Expression.Convert(_exprStack.Pop(), typeof(object)), resultType);
                    break;
                case ILOpCode.Initobj:
                    e = _exprStack.Pop();
                    Debug.Assert(resultType == e.Type);
                    return;
                case ILOpCode.Isinst:
                    e = Expression.TypeIs(_exprStack.Pop(), resultType);
                    break;
                case ILOpCode.Newarr:
                    var index = _exprStack.Pop() as ConstantExpression;
                    if (index == null || index.Type != typeof(int))
                        throw NotLambda(opCode);

                    var initializers = new Expression[(int) index.Value];
                    var @null = CreateDefaultConstant(resultType);
                    for (int i = 0; i < initializers.Length; i++)
                        initializers[i] = @null;

                    e = Expression.NewArrayInit(resultType, initializers);
                    break;
                default:
                    throw NotLambda(opCode);
            }

            _exprStack.Push(e);
        }

        public void VisitVarInsn(ILOpCode opCode, int var) {
            Expression e;
            switch (opCode) {
                case ILOpCode.Ldarga:
                case ILOpCode.Ldarga_s:
                case ILOpCode.Ldarg:
                case ILOpCode.Ldarg_s:
                case ILOpCode.Ldarg_0:
                case ILOpCode.Ldarg_1:
                case ILOpCode.Ldarg_2:
                case ILOpCode.Ldarg_3:
                    if (_target != null) {
                        if (var == 0) {
                            _exprStack.Push(_target);
                            return;
                        }

                        var--;
                    }

                    _exprStack.Push(_arguments?[var] ?? _params[var]);
                    return;

                default:
                case ILOpCode.Starg:
                case ILOpCode.Starg_s:
                    throw NotLambda(opCode);

                case ILOpCode.Ldloca:
                case ILOpCode.Ldloca_s:
                    var localVariable = _exprStack.LocalVariables[var];
                    e = localVariable.Get(_statements, _variables);
                    if (e == null) {
                        // ReSharper disable once AssignNullToNotNullAttribute
                        e = Expression.Constant(null, localVariable.Info.LocalType);
                        _exprStack.LocalVariables[var].Assign(e, _exprStack);
                    }

                    _exprStack.Push(e);
                    return;
                case ILOpCode.Ldloc:
                case ILOpCode.Ldloc_s:
                case ILOpCode.Ldloc_0:
                case ILOpCode.Ldloc_1:
                case ILOpCode.Ldloc_2:
                case ILOpCode.Ldloc_3:
                    e = _exprStack.LocalVariables[var].Get(_statements, _variables);
                    Debug.Assert(e != null);

                    _exprStack.Push(e);
                    return;

                case ILOpCode.Stloc:
                case ILOpCode.Stloc_s:
                case ILOpCode.Stloc_0:
                case ILOpCode.Stloc_1:
                case ILOpCode.Stloc_2:
                case ILOpCode.Stloc_3:

                    _exprStack.LocalVariables[var].Assign(_exprStack.Pop(), _exprStack);
                    return;
            }
        }

        static ArgumentException NotLambda(ILOpCode opCode) {
            return new ArgumentException($"Not a lambda expression. OpCode {opCode} is illegal.");
        }
    }
}
