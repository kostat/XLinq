using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Streamx.Linq.ExTree {
    public static class ExpressionTree {
        private const Byte DOUBLE_BYTE_OP_CODE_MARK = 0xFE;

        public static Expression<TDelegate> Parse<TDelegate>(TDelegate @delegate) where TDelegate : MulticastDelegate {
            var method = @delegate.Method;
            var target = @delegate.Target;

            return Parse<TDelegate>(target, method);
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public static Expression<TDelegate> Parse<TDelegate>(object target, MethodInfo method) where TDelegate : MulticastDelegate {
            var targetExpression = method.IsStatic ? null : Expression.Constant(target);
            return (Expression<TDelegate>) Parse(typeof(TDelegate), targetExpression, method);
        }

        public static LambdaExpression Parse(Type delegateType, Expression targetExpression, MethodInfo method, IList<Expression> arguments = null) {
            var parameters = method.GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToList();
            var visitor = new MethodVisitor(targetExpression, parameters, method.ReturnType, arguments);

            Visit(visitor, method);

            var body = Expression.Block(method.ReturnType, visitor.Variables, visitor.Statements);
            return Expression.Lambda(delegateType, body, parameters);
        }

        public static LambdaExpression Parse(Expression targetExpression, MethodInfo method, IList<Expression> arguments = null) {
            var @params = method.GetParameters();
            var arraySize = @params.Length;
            var returnType = method.ReturnType;
            var isAction = returnType == typeof(void);
            if (!isAction)
                arraySize++;

            var typeArgs = new Type[arraySize];
            for (int i = 0; i < @params.Length; i++)
                typeArgs[i] = @params[i].ParameterType;

            Type delegateType;
            if (isAction)
                delegateType = Expression.GetActionType(typeArgs);
            else {
                typeArgs[@params.Length] = returnType;
                delegateType = Expression.GetFuncType(typeArgs);
            }

            return Parse(delegateType, targetExpression, method, arguments);
        }

        private static bool Jump(MethodVisitor visitor, ILOpCode opCode, int offset) =>
            visitor.VisitJumpInsn(opCode, new Label(offset));

        private static void Visit(MethodVisitor visitor, MethodInfo method) {
            var methodBody = method.GetMethodBody();
            Debug.Assert(methodBody != null);

            visitor.VisitCode();
            visitor.VisitMaxs(methodBody.MaxStackSize, methodBody.LocalVariables);

            var module = method.Module;
            var ilAsByteArray = methodBody.GetILAsByteArray();
            var declaringType = method.DeclaringType;
            var typeArgs = declaringType.IsGenericType ? declaringType.GetGenericArguments() : null;
            var methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null;
            using (var ilStream = new MemoryStream(ilAsByteArray))
            using (var reader = new BinaryReader(ilStream)) {
                var labels = new List<int>();

                while (ilStream.Position < ilAsByteArray.Length) {
                    if (labels.Contains((int) ilStream.Position))
                        visitor.VisitLabel(new Label((int) ilStream.Position));

                    var instr = reader.ReadByte();
                    var opCode = (ILOpCode) (instr == DOUBLE_BYTE_OP_CODE_MARK
                        ? DOUBLE_BYTE_OP_CODE_MARK << 8 | reader.ReadByte()
                        : instr);

                    switch (opCode) {
                        case ILOpCode.Ldelem:
                        case ILOpCode.Stelem:
                            reader.ReadInt32();
                            visitor.VisitInsn(opCode);
                            break;

                        case ILOpCode.Add:
                        case ILOpCode.Add_ovf:
                        case ILOpCode.Add_ovf_un:
                        case ILOpCode.And:
                        case ILOpCode.Ceq:
                        case ILOpCode.Cgt:
                        case ILOpCode.Cgt_un:
                        case ILOpCode.Clt:
                        case ILOpCode.Clt_un:
                        case ILOpCode.Conv_i1:
                        case ILOpCode.Conv_i2:
                        case ILOpCode.Conv_i4:
                        case ILOpCode.Conv_i8:
                        case ILOpCode.Conv_ovf_i1:
                        case ILOpCode.Conv_ovf_i1_un:
                        case ILOpCode.Conv_ovf_i2:
                        case ILOpCode.Conv_ovf_i2_un:
                        case ILOpCode.Conv_ovf_i4:
                        case ILOpCode.Conv_ovf_i4_un:
                        case ILOpCode.Conv_ovf_i8:
                        case ILOpCode.Conv_ovf_i8_un:
                        case ILOpCode.Conv_ovf_u1:
                        case ILOpCode.Conv_ovf_u1_un:
                        case ILOpCode.Conv_ovf_u2:
                        case ILOpCode.Conv_ovf_u2_un:
                        case ILOpCode.Conv_ovf_u4:
                        case ILOpCode.Conv_ovf_u4_un:
                        case ILOpCode.Conv_ovf_u8:
                        case ILOpCode.Conv_ovf_u8_un:
                        case ILOpCode.Conv_r_un:
                        case ILOpCode.Conv_r4:
                        case ILOpCode.Conv_r8:

                        case ILOpCode.Conv_u1:
                        case ILOpCode.Conv_u2:
                        case ILOpCode.Conv_u4:
                        case ILOpCode.Conv_u8:
                        case ILOpCode.Div:
                        case ILOpCode.Div_un:
                        case ILOpCode.Dup:

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
                        case ILOpCode.Ldlen:
                        case ILOpCode.Mul:
                        case ILOpCode.Mul_ovf:
                        case ILOpCode.Mul_ovf_un:
                        case ILOpCode.Neg:
                        case ILOpCode.Not:
                        case ILOpCode.Or:
                        case ILOpCode.Pop:
                        case ILOpCode.Rem:
                        case ILOpCode.Rem_un:
                        case ILOpCode.Ret:
                        case ILOpCode.Shl:
                        case ILOpCode.Shr:
                        case ILOpCode.Shr_un:

                        case ILOpCode.Stelem_i1:
                        case ILOpCode.Stelem_i2:
                        case ILOpCode.Stelem_i4:
                        case ILOpCode.Stelem_i8:
                        case ILOpCode.Stelem_r4:
                        case ILOpCode.Stelem_r8:
                        case ILOpCode.Stelem_ref:
                        case ILOpCode.Sub:
                        case ILOpCode.Sub_ovf:
                        case ILOpCode.Sub_ovf_un:
                        case ILOpCode.Xor:
                            visitor.VisitInsn(opCode);
                            break;

                        case ILOpCode.Beq:
                        case ILOpCode.Bge:
                        case ILOpCode.Bge_un:
                        case ILOpCode.Bgt:
                        case ILOpCode.Bgt_un:
                        case ILOpCode.Ble:
                        case ILOpCode.Ble_un:
                        case ILOpCode.Blt:
                        case ILOpCode.Blt_un:
                        case ILOpCode.Bne_un:
                        case ILOpCode.Br:
                        case ILOpCode.Brfalse:
                        case ILOpCode.Brtrue:
                            var offset = reader.ReadInt32() + (int) ilStream.Position;
                            labels.Add(offset);
                            if (Jump(visitor, opCode, offset))
                                ilStream.Position = offset;
                            break;

                        case ILOpCode.Beq_s:
                        case ILOpCode.Bge_s:
                        case ILOpCode.Bge_un_s:
                        case ILOpCode.Bgt_s:
                        case ILOpCode.Bgt_un_s:
                        case ILOpCode.Ble_s:
                        case ILOpCode.Ble_un_s:
                        case ILOpCode.Blt_s:
                        case ILOpCode.Blt_un_s:
                        case ILOpCode.Bne_un_s:
                        case ILOpCode.Br_s:
                        case ILOpCode.Brfalse_s:
                        case ILOpCode.Brtrue_s:
                            var offset1 = reader.ReadSByte() + (int) ilStream.Position;
                            labels.Add(offset1);
                            if (Jump(visitor, opCode, offset1))
                                ilStream.Position = offset1;
                            break;

                        case ILOpCode.Box:
                        case ILOpCode.Castclass:
                        case ILOpCode.Constrained:
                        case ILOpCode.Initobj:
                        case ILOpCode.Isinst:
                        case ILOpCode.Newarr:
                        case ILOpCode.Unbox:
                        case ILOpCode.Unbox_any:

                            visitor.VisitTypeInsn(opCode, module.ResolveType(reader.ReadInt32(), typeArgs, methodArgs));
                            break;

                        case ILOpCode.Call:
                        case ILOpCode.Calli:
                        case ILOpCode.Callvirt:
                        case ILOpCode.Ldftn:
                        case ILOpCode.Newobj:

                            visitor.VisitMethodInsn(opCode, module.ResolveMethod(reader.ReadInt32(), typeArgs, methodArgs));
                            break;

                        case ILOpCode.Ldarg:
                        case ILOpCode.Ldarga:
                        case ILOpCode.Ldloc:
                        case ILOpCode.Ldloca: // this before call
                        case ILOpCode.Starg:
                        case ILOpCode.Stloc:
                            visitor.VisitVarInsn(opCode, reader.ReadUInt16());
                            break;

                        case ILOpCode.Ldarga_s:
                        case ILOpCode.Ldarg_s:
                        case ILOpCode.Ldloc_s:
                        case ILOpCode.Ldloca_s: // this before call
                        case ILOpCode.Starg_s:
                        case ILOpCode.Stloc_s:
                            visitor.VisitVarInsn(opCode, reader.ReadByte());
                            break;

                        case ILOpCode.Ldarg_0:
                        case ILOpCode.Ldarg_1:
                        case ILOpCode.Ldarg_2:
                        case ILOpCode.Ldarg_3:
                            visitor.VisitVarInsn(opCode, opCode - ILOpCode.Ldarg_0);
                            break;

                        case ILOpCode.Ldloc_0:
                        case ILOpCode.Ldloc_1:
                        case ILOpCode.Ldloc_2:
                        case ILOpCode.Ldloc_3:
                            visitor.VisitVarInsn(opCode, opCode - ILOpCode.Ldloc_0);
                            break;

                        case ILOpCode.Stloc_0:
                        case ILOpCode.Stloc_1:
                        case ILOpCode.Stloc_2:
                        case ILOpCode.Stloc_3:
                            visitor.VisitVarInsn(opCode, opCode - ILOpCode.Stloc_0);
                            break;

                        case ILOpCode.Ldc_i4:
                            visitor.VisitLdcInsn(reader.ReadInt32());
                            break;

                        case ILOpCode.Ldc_i4_s:
                            visitor.VisitLdcInsn((int) reader.ReadByte());
                            break;

                        case ILOpCode.Ldc_i8:
                            visitor.VisitLdcInsn(reader.ReadInt64());
                            break;

                        case ILOpCode.Ldc_r4:
                            visitor.VisitLdcInsn(reader.ReadSingle());
                            break;

                        case ILOpCode.Ldc_r8:
                            visitor.VisitLdcInsn(reader.ReadDouble());
                            break;

                        case ILOpCode.Ldstr:
                            visitor.VisitLdcInsn(module.ResolveString(reader.ReadInt32()));
                            break;

                        case ILOpCode.Ldvirtftn:
                            visitor.VisitLdcInsn(module.ResolveMethod(reader.ReadInt32(), typeArgs, methodArgs));
                            break;

                        case ILOpCode.Ldnull:
                            visitor.VisitLdcInsn(null);
                            break;

                        case ILOpCode.Ldc_i4_0:
                        case ILOpCode.Ldc_i4_1:
                        case ILOpCode.Ldc_i4_2:
                        case ILOpCode.Ldc_i4_3:
                        case ILOpCode.Ldc_i4_4:
                        case ILOpCode.Ldc_i4_5:
                        case ILOpCode.Ldc_i4_6:
                        case ILOpCode.Ldc_i4_7:
                        case ILOpCode.Ldc_i4_8:
                        case ILOpCode.Ldc_i4_m1:
                            visitor.VisitLdcInsn((int) (opCode - ILOpCode.Ldc_i4_0));
                            break;

                        case ILOpCode.Ldfld:
                        case ILOpCode.Ldflda:
                        case ILOpCode.Ldsfld:
                        case ILOpCode.Ldsflda:
                        case ILOpCode.Stfld:
                        case ILOpCode.Stsfld:
                            visitor.VisitFieldInsn(opCode, module.ResolveField(reader.ReadInt32(), typeArgs, methodArgs));
                            break;

                        // no ops
                        case ILOpCode.Break:
                        case ILOpCode.Nop:
                        case ILOpCode.Tail:
                        case ILOpCode.Volatile:
                        default:
                            break;

                        case ILOpCode.Arglist:
                        case ILOpCode.Ckfinite:
                        case ILOpCode.Conv_i:
                        case ILOpCode.Conv_ovf_i:
                        case ILOpCode.Conv_ovf_i_un:
                        case ILOpCode.Conv_ovf_u:
                        case ILOpCode.Conv_ovf_u_un:
                        case ILOpCode.Conv_u:
                        case ILOpCode.Cpblk:
                        case ILOpCode.Cpobj:
                        case ILOpCode.Endfilter:
                        case ILOpCode.Endfinally:
                        case ILOpCode.Initblk:
                        case ILOpCode.Jmp:
                        case ILOpCode.Ldelema:
                        case ILOpCode.Ldind_i:
                        case ILOpCode.Ldind_i1:
                        case ILOpCode.Ldind_i2:
                        case ILOpCode.Ldind_i4:
                        case ILOpCode.Ldind_i8:
                        case ILOpCode.Ldind_r4:
                        case ILOpCode.Ldind_r8:
                        case ILOpCode.Ldind_ref:
                        case ILOpCode.Ldind_u1:
                        case ILOpCode.Ldind_u2:
                        case ILOpCode.Ldind_u4:
                        case ILOpCode.Ldobj:
                        case ILOpCode.Ldtoken:
                        case ILOpCode.Ldelem_i:
                        case ILOpCode.Leave:
                        case ILOpCode.Leave_s:
                        case ILOpCode.Localloc:
                        case ILOpCode.Mkrefany:
                        case ILOpCode.Refanytype:
                        case ILOpCode.Refanyval:
                        case ILOpCode.Rethrow:
                        case ILOpCode.Sizeof:
                        case ILOpCode.Stelem_i:
                        case ILOpCode.Stind_i:
                        case ILOpCode.Stind_i1:
                        case ILOpCode.Stind_i2:
                        case ILOpCode.Stind_i4:
                        case ILOpCode.Stind_i8:
                        case ILOpCode.Stind_r4:
                        case ILOpCode.Stind_r8:
                        case ILOpCode.Stind_ref:
                        case ILOpCode.Stobj:
                        case ILOpCode.Switch:
                        case ILOpCode.Throw:
                        case ILOpCode.Unaligned:
                            //NA
                            throw new ArgumentException("unsupported opCode: " + opCode);
                    }
                }
            }

            visitor.VisitEnd();
        }
    }
}
