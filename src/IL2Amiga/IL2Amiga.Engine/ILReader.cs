using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using IL2Amiga.Engine.Extensions;
using IL2Amiga.Engine.MethodAnalysis;

namespace IL2Amiga.Engine
{
    public class ILReader
    {
        // We split this into two arrays since we have to read
        // a byte at a time anyways. In the future if we need to
        // back to a unified array, instead of 64k entries
        // we can change it to a signed int, and then add x0200 to the value.
        // This will reduce array size down to 768 entries.
        static readonly ImmutableArray<OpCode> opCodesLo;
        static readonly ImmutableArray<OpCode> opCodesHi;

        static ILReader()
        {
            (opCodesLo, opCodesHi) = LoadOpCodes();
        }

        static (ImmutableArray<OpCode> Lo, ImmutableArray<OpCode> Hi) LoadOpCodes()
        {
            var lo = new OpCode[256];
            var hi = new OpCode[256];
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public))
            {
                var opCode = (OpCode)field.GetValue(null)!;
                var value = (ushort)opCode.Value;
                if (value <= 0xFF)
                {
                    lo[value] = opCode;
                }
                else
                {
                    hi[value & 0xFF] = opCode;
                }
            }
            return (lo.ToImmutableArray(), hi.ToImmutableArray());
        }

        void CheckBranch(int target, int methodSize)
        {
            // this method is a safety-measure. Should never occur
            if (target < 0 || target >= methodSize)
            {
                throw new Exception("Branch jumps outside method.");
            }
        }

        public ImmutableArray<ILOpCode> ProcessMethod(MethodBase method)
        {
            var result = new List<ILOpCode>();

            var body = method.GetMethodBody();
            var module = method.Module;

            // Cache for use in field and method resolution
            Type[] typeGenArgs = Type.EmptyTypes;
            Type[] methodGenArgs = Type.EmptyTypes;
            if (method.DeclaringType!.IsGenericType)
            {
                typeGenArgs = method.DeclaringType.GetGenericArguments();
            }
            if (method.IsGenericMethod)
            {
                methodGenArgs = method.GetGenericArguments();
            }

            #region Unsafe Intrinsic

            if (method.DeclaringType.FullName == "Internal.Runtime.CompilerServices.Unsafe")
            {
                var unsafeType = Type.GetType("System.Runtime.CompilerServices.Unsafe, System.Runtime.CompilerServices.Unsafe") ?? throw new Exception("Couldn't find unsafe");
                var unsafeMethod = unsafeType.GetMethods()
                    .Where(
                        m => m.Name == method.Name
                        && m.GetGenericArguments().Length == method.GetGenericArguments().Length
                        && m.GetParameters().Length == method.GetParameters().Length)
                    .SingleOrDefault(
                        m =>
                        {
                            var paramTypes = Array.ConvertAll(m.GetParameters(), p => p.ParameterType);
                            var originalParamTypes = Array.ConvertAll(
                                ((MethodInfo)method).GetParameters(), p => p.ParameterType);

                            for (int i = 0; i < paramTypes.Length; i++)
                            {
                                var paramType = paramTypes[i];
                                var originalParamType = originalParamTypes[i];

                                while (paramType!.HasElementType)
                                {
                                    if (!originalParamType!.HasElementType)
                                    {
                                        return false;
                                    }

                                    if ((paramType.IsArray && !originalParamType.IsArray)
                                        || (paramType.IsByRef && !originalParamType.IsByRef)
                                        || (paramType.IsPointer && !originalParamType.IsPointer))
                                    {
                                        return false;
                                    }

                                    paramType = paramType.GetElementType();
                                    originalParamType = originalParamType.GetElementType();
                                }

                                if (!paramType.IsAssignableFrom(originalParamType)
                                    && (!paramType.IsGenericParameter || (paramType.HasElementType && !paramType.IsArray)))
                                {
                                    return false;
                                }
                            }

                            return true;
                        });

                if (unsafeMethod is not null)
                {
                    body = unsafeMethod.GetMethodBody();
                    module = unsafeMethod.Module;
                }
            }

            #endregion

            #region ByReference Intrinsic

            if (method.DeclaringType.IsGenericType
                && method.DeclaringType.GetGenericTypeDefinition().FullName == "System.ByReference`1")
            {
                var valueField = method.DeclaringType.GetField("_value", BindingFlags.Instance | BindingFlags.NonPublic);

                switch (method.Name)
                {
                    case ".ctor":

                        // push $this
                        result.Add(new ILOpCodes.OpVar(ILOpCode.Code.Ldarg, 0, 1, 0, null));

                        // push value (arg 1)
                        result.Add(new ILOpCodes.OpVar(ILOpCode.Code.Ldarg, 1, 2, 1, null));

                        // store value into $this._value
                        result.Add(new ILOpCodes.OpField(ILOpCode.Code.Stfld, 2, 8, valueField, null));

                        // return
                        result.Add(new ILOpCodes.OpNone(ILOpCode.Code.Ret, 8, 9, null));

                        break;

                    case "get_Value":

                        // push $this
                        result.Add(new ILOpCodes.OpVar(ILOpCode.Code.Ldarg, 0, 1, 0, null));

                        // push $this._value
                        result.Add(new ILOpCodes.OpField(ILOpCode.Code.Ldfld, 1, 6, valueField, null));

                        // return
                        result.Add(new ILOpCodes.OpNone(ILOpCode.Code.Ret, 6, 7, null));

                        break;

                    default:
                        throw new NotImplementedException($"ByReference intrinsic method '{method}' not implemented!");
                }

                foreach (var op in result)
                {
                    op.InitStackAnalysis(method);
                }

                return result.ToImmutableArray();
            }

            #endregion

            #region RuntimeTypeHandle

            if (method.DeclaringType.Name == "RuntimeType")
            {
                if (method.Name == ".ctor")
                {
                    var op = new ILOpCodes.OpNone(ILOpCode.Code.Ret, 0, 1, null);
                    op.InitStackAnalysis(method);

                    result.Add(op);

                    return result.ToImmutableArray();
                }
            }

            if (method.DeclaringType.Name == "TypeImpl")
            {
                if (method.Name == "CreateRuntimeTypeHandle")
                {
                    // the idea of this method is to first create a RuntimeType object, set its handle and then create a RuntimeTypeHandle from it
                    // we are manually coding in il here since we have to call a internal method on an internal class
                    var runtimeType = Type.GetType("System.RuntimeType") ?? throw new NullReferenceException();
                    var ctor = runtimeType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { }, null);
                    result.Add(new ILOpCodes.OpMethod(ILOpCode.Code.Newobj, 0, 1, ctor, null)
                    {
                        StackPopTypes = Array.Empty<Type>(),
                        StackPushTypes = new[] { runtimeType },
                    });
                    result.Add(new ILOpCodes.OpNone(ILOpCode.Code.Dup, 1, 2, null)
                    {
                        StackPopTypes = new[] { runtimeType },
                        StackPushTypes = new[] { runtimeType, runtimeType }
                    });
                    result.Add(new ILOpCodes.OpVar(ILOpCode.Code.Ldarg, 2, 3, 0, null)
                    {
                        StackPopTypes = Array.Empty<Type>(),
                        StackPushTypes = new[] { typeof(int) },
                    });
                    var m_handle = runtimeType.GetField("m_handle", BindingFlags.Instance | BindingFlags.NonPublic);
                    result.Add(new ILOpCodes.OpField(ILOpCode.Code.Stfld, 3, 4, m_handle, null)
                    {
                        StackPopTypes = new[] { typeof(int), runtimeType },
                        StackPushTypes = Array.Empty<Type>(),
                    });
                    var runtimeTypeHandle = Type.GetType("System.RuntimeTypeHandle") ?? throw new NullReferenceException();
                    ctor = runtimeTypeHandle.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { runtimeType }, null);
                    result.Add(new ILOpCodes.OpMethod(ILOpCode.Code.Newobj, 4, 5, ctor, null)
                    {
                        StackPopTypes = new[] { runtimeType },
                        StackPushTypes = new[] { runtimeTypeHandle },
                    });
                    result.Add(new ILOpCodes.OpNone(ILOpCode.Code.Ret, 5, 6, null)
                    {
                        StackPopTypes = Array.Empty<Type>(),
                        StackPushTypes = Array.Empty<Type>(),
                    });

                    return result.ToImmutableArray();
                }
            }
            #endregion

            #region ArrayPool ("hacked" generic plug)

            if (method.DeclaringType.IsGenericType
                && method.DeclaringType.GetGenericTypeDefinition().FullName == "System.Buffers.ArrayPool`1")
            {
                if (method.Name == ".cctor")
                {
                    var op = new ILOpCodes.OpNone(ILOpCode.Code.Ret, 0, 1, null);
                    op.InitStackAnalysis(method);

                    result.Add(op);

                    return result.ToImmutableArray();
                }
            }

            #endregion

            #region RuntimeHelpers

            if (method.DeclaringType.FullName == "System.Runtime.CompilerServices.RuntimeHelpers")
            {
                if (method.Name == "IsBitwiseEquatable")
                {
                    // This is a generic method so we emit true or false depending on the type
                    ILOpCode op;
                    if (ILOp.IsIntegralTypeOrPointer(methodGenArgs[0]))
                    {
                        op = new ILOpCodes.OpInt(ILOpCode.Code.Ldc_I4, 0, 1, 1, null);
                    }
                    else
                    {
                        op = new ILOpCodes.OpInt(ILOpCode.Code.Ldc_I4, 0, 1, 1, null);
                    }
                    op.InitStackAnalysis(method);
                    result.Add(op);

                    op = new ILOpCodes.OpNone(ILOpCode.Code.Ret, 1, 2, null);
                    op.InitStackAnalysis(method);

                    result.Add(op);

                    return result.ToImmutableArray();
                }
            }

            #endregion

            // Some methods return no body. Not sure why.. have to investigate
            // They arent abstracts or icalls...
            if (body is null)
            {
                return result.ToImmutableArray();
            }

            var il = body.GetILAsByteArray() ?? throw new NullReferenceException();
            int xPos = 0;
            while (xPos < il.Length)
            {
                ExceptionRegionInfoEx? xCurrentExceptionRegion = null;
                #region Determine current handler
                // todo: add support for nested handlers using a stack or so..
                foreach (ExceptionRegionInfoEx xHandler in method.GetExceptionRegionInfos())
                {
                    if (xHandler.TryOffset >= 0)
                    {
                        if (xHandler.TryOffset <= xPos && (xHandler.TryLength + xHandler.TryOffset) > xPos)
                        {
                            if (xCurrentExceptionRegion == null)
                            {
                                xCurrentExceptionRegion = xHandler;
                                continue;
                            }
                            else if (xHandler.TryOffset > xCurrentExceptionRegion.TryOffset && (xHandler.TryLength + xHandler.TryOffset) < (xCurrentExceptionRegion.TryLength + xCurrentExceptionRegion.TryOffset))
                            {
                                // only replace if the current found handler is narrower
                                xCurrentExceptionRegion = xHandler;
                                continue;
                            }
                        }
                    }
                    // todo: handler offset can be 0 like try offset?
                    if (xHandler.HandlerOffset > 0)
                    {
                        if (xHandler.HandlerOffset <= xPos && (xHandler.HandlerOffset + xHandler.HandlerLength) > xPos)
                        {
                            if (xCurrentExceptionRegion == null)
                            {
                                xCurrentExceptionRegion = xHandler;
                                continue;
                            }
                            else if (xHandler.HandlerOffset > xCurrentExceptionRegion.HandlerOffset && (xHandler.HandlerOffset + xHandler.HandlerLength) < (xCurrentExceptionRegion.HandlerOffset + xCurrentExceptionRegion.HandlerLength))
                            {
                                // only replace if the current found handler is narrower
                                xCurrentExceptionRegion = xHandler;
                                continue;
                            }
                        }
                    }
                    if (xHandler.Kind.HasFlag(ExceptionRegionKind.Filter))
                    {
                        if (xHandler.FilterOffset > 0)
                        {
                            if (xHandler.FilterOffset <= xPos)
                            {
                                if (xCurrentExceptionRegion == null)
                                {
                                    xCurrentExceptionRegion = xHandler;
                                    continue;
                                }
                                else if (xHandler.FilterOffset > xCurrentExceptionRegion.FilterOffset)
                                {
                                    // only replace if the current found handler is narrower
                                    xCurrentExceptionRegion = xHandler;
                                    continue;
                                }
                            }
                        }
                    }
                }
                #endregion
                ILOpCode.Code xOpCodeVal;
                OpCode xOpCode;
                int xOpPos = xPos;
                if (il[xPos] == 0xFE)
                {
                    xOpCodeVal = (ILOpCode.Code)(0xFE00 | il[xPos + 1]);
                    xOpCode = opCodesHi[il[xPos + 1]];
                    xPos = xPos + 2;
                }
                else
                {
                    xOpCodeVal = (ILOpCode.Code)il[xPos];
                    xOpCode = opCodesLo[il[xPos]];
                    xPos++;
                }

                ILOpCode? xILOpCode = null;
                switch (xOpCode.OperandType)
                {
                    // No operand.
                    case OperandType.InlineNone:
                        {
                            #region Inline none options
                            // These shortcut translation regions expand shortcut ops into full ops
                            // This eliminates the amount of code required in the assemblers
                            // by allowing them to ignore the shortcuts
                            switch (xOpCodeVal)
                            {
                                case ILOpCode.Code.Ldarg_0:
                                    xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Ldarg, xOpPos, xPos, 0, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldarg_1:
                                    xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Ldarg, xOpPos, xPos, 1, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldarg_2:
                                    xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Ldarg, xOpPos, xPos, 2, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldarg_3:
                                    xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Ldarg, xOpPos, xPos, 3, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldc_I4_0:
                                    xILOpCode = new ILOpCodes.OpInt(ILOpCode.Code.Ldc_I4, xOpPos, xPos, 0, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldc_I4_1:
                                    xILOpCode = new ILOpCodes.OpInt(ILOpCode.Code.Ldc_I4, xOpPos, xPos, 1, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldc_I4_2:
                                    xILOpCode = new ILOpCodes.OpInt(ILOpCode.Code.Ldc_I4, xOpPos, xPos, 2, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldc_I4_3:
                                    xILOpCode = new ILOpCodes.OpInt(ILOpCode.Code.Ldc_I4, xOpPos, xPos, 3, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldc_I4_4:
                                    xILOpCode = new ILOpCodes.OpInt(ILOpCode.Code.Ldc_I4, xOpPos, xPos, 4, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldc_I4_5:
                                    xILOpCode = new ILOpCodes.OpInt(ILOpCode.Code.Ldc_I4, xOpPos, xPos, 5, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldc_I4_6:
                                    xILOpCode = new ILOpCodes.OpInt(ILOpCode.Code.Ldc_I4, xOpPos, xPos, 6, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldc_I4_7:
                                    xILOpCode = new ILOpCodes.OpInt(ILOpCode.Code.Ldc_I4, xOpPos, xPos, 7, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldc_I4_8:
                                    xILOpCode = new ILOpCodes.OpInt(ILOpCode.Code.Ldc_I4, xOpPos, xPos, 8, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldc_I4_M1:
                                    xILOpCode = new ILOpCodes.OpInt(ILOpCode.Code.Ldc_I4, xOpPos, xPos, -1, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldloc_0:
                                    xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Ldloc, xOpPos, xPos, 0, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldloc_1:
                                    xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Ldloc, xOpPos, xPos, 1, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldloc_2:
                                    xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Ldloc, xOpPos, xPos, 2, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ldloc_3:
                                    xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Ldloc, xOpPos, xPos, 3, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Stloc_0:
                                    xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Stloc, xOpPos, xPos, 0, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Stloc_1:
                                    xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Stloc, xOpPos, xPos, 1, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Stloc_2:
                                    xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Stloc, xOpPos, xPos, 2, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Stloc_3:
                                    xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Stloc, xOpPos, xPos, 3, xCurrentExceptionRegion);
                                    break;
                                default:
                                    xILOpCode = new ILOpCodes.OpNone(xOpCodeVal, xOpPos, xPos, xCurrentExceptionRegion);
                                    break;
                            }
                            #endregion
                            break;
                        }

                    case OperandType.ShortInlineBrTarget:
                        {
                            #region Inline branch
                            // By calculating target, we assume all branches are within a method
                            // So far at least wtih csc, its true. We check it with CheckBranch
                            // just in case.
                            int xTarget = xPos + 1 + (sbyte)il[xPos];
                            CheckBranch(xTarget, il.Length);
                            switch (xOpCodeVal)
                            {
                                case ILOpCode.Code.Beq_S:
                                    xILOpCode = new ILOpCodes.OpBranch(ILOpCode.Code.Beq, xOpPos, xPos + 1, xTarget, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Bge_S:
                                    xILOpCode = new ILOpCodes.OpBranch(ILOpCode.Code.Bge, xOpPos, xPos + 1, xTarget, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Bge_Un_S:
                                    xILOpCode = new ILOpCodes.OpBranch(ILOpCode.Code.Bge_Un, xOpPos, xPos + 1, xTarget, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Bgt_S:
                                    xILOpCode = new ILOpCodes.OpBranch(ILOpCode.Code.Bgt, xOpPos, xPos + 1, xTarget, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Bgt_Un_S:
                                    xILOpCode = new ILOpCodes.OpBranch(ILOpCode.Code.Bgt_Un, xOpPos, xPos + 1, xTarget, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ble_S:
                                    xILOpCode = new ILOpCodes.OpBranch(ILOpCode.Code.Ble, xOpPos, xPos + 1, xTarget, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Ble_Un_S:
                                    xILOpCode = new ILOpCodes.OpBranch(ILOpCode.Code.Ble_Un, xOpPos, xPos + 1, xTarget, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Blt_S:
                                    xILOpCode = new ILOpCodes.OpBranch(ILOpCode.Code.Blt, xOpPos, xPos + 1, xTarget, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Blt_Un_S:
                                    xILOpCode = new ILOpCodes.OpBranch(ILOpCode.Code.Blt_Un, xOpPos, xPos + 1, xTarget, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Bne_Un_S:
                                    xILOpCode = new ILOpCodes.OpBranch(ILOpCode.Code.Bne_Un, xOpPos, xPos + 1, xTarget, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Br_S:
                                    xILOpCode = new ILOpCodes.OpBranch(ILOpCode.Code.Br, xOpPos, xPos + 1, xTarget, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Brfalse_S:
                                    xILOpCode = new ILOpCodes.OpBranch(ILOpCode.Code.Brfalse, xOpPos, xPos + 1, xTarget, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Brtrue_S:
                                    xILOpCode = new ILOpCodes.OpBranch(ILOpCode.Code.Brtrue, xOpPos, xPos + 1, xTarget, xCurrentExceptionRegion);
                                    break;
                                case ILOpCode.Code.Leave_S:
                                    xILOpCode = new ILOpCodes.OpBranch(ILOpCode.Code.Leave, xOpPos, xPos + 1, xTarget, xCurrentExceptionRegion);
                                    break;
                                default:
                                    xILOpCode = new ILOpCodes.OpBranch(xOpCodeVal, xOpPos, xPos + 1, xTarget, xCurrentExceptionRegion);
                                    break;
                            }
                            xPos = xPos + 1;
                            break;
                            #endregion
                        }
                    case OperandType.InlineBrTarget:
                        {
                            int xTarget = xPos + 4 + ReadInt32(il, xPos);
                            CheckBranch(xTarget, il.Length);
                            xILOpCode = new ILOpCodes.OpBranch(xOpCodeVal, xOpPos, xPos + 4, xTarget, xCurrentExceptionRegion);
                            xPos = xPos + 4;
                            break;
                        }

                    case OperandType.ShortInlineI:
                        switch (xOpCodeVal)
                        {
                            case ILOpCode.Code.Ldc_I4_S:
                                xILOpCode = new ILOpCodes.OpInt(ILOpCode.Code.Ldc_I4, xOpPos, xPos + 1, ((sbyte)il[xPos]), xCurrentExceptionRegion);
                                break;
                            default:
                                xILOpCode = new ILOpCodes.OpInt(xOpCodeVal, xOpPos, xPos + 1, ((sbyte)il[xPos]), xCurrentExceptionRegion);
                                break;
                        }
                        xPos = xPos + 1;
                        break;
                    case OperandType.InlineI:
                        xILOpCode = new ILOpCodes.OpInt(xOpCodeVal, xOpPos, xPos + 4, ReadInt32(il, xPos), xCurrentExceptionRegion);
                        xPos = xPos + 4;
                        break;
                    case OperandType.InlineI8:
                        xILOpCode = new ILOpCodes.OpInt64(xOpCodeVal, xOpPos, xPos + 8, ReadUInt64(il, xPos), xCurrentExceptionRegion);
                        xPos = xPos + 8;
                        break;

                    case OperandType.ShortInlineR:
                        xILOpCode = new ILOpCodes.OpSingle(xOpCodeVal, xOpPos, xPos + 4, BitConverter.ToSingle(il, xPos), xCurrentExceptionRegion);
                        xPos = xPos + 4;
                        break;
                    case OperandType.InlineR:
                        xILOpCode = new ILOpCodes.OpDouble(xOpCodeVal, xOpPos, xPos + 8, BitConverter.ToDouble(il, xPos), xCurrentExceptionRegion);
                        xPos = xPos + 8;
                        break;

                    // The operand is a 32-bit metadata token.
                    case OperandType.InlineField:
                        {
                            var xValue = module.ResolveField(ReadInt32(il, xPos), typeGenArgs, methodGenArgs);
                            xILOpCode = new ILOpCodes.OpField(xOpCodeVal, xOpPos, xPos + 4, xValue, xCurrentExceptionRegion);
                            xPos = xPos + 4;
                            break;
                        }

                    // The operand is a 32-bit metadata token.
                    case OperandType.InlineMethod:
                        {
                            var xValue = module.ResolveMethod(ReadInt32(il, xPos), typeGenArgs, methodGenArgs);
                            xILOpCode = new ILOpCodes.OpMethod(xOpCodeVal, xOpPos, xPos + 4, xValue, xCurrentExceptionRegion);
                            xPos = xPos + 4;
                            break;
                        }

                    // 32-bit metadata signature token.
                    case OperandType.InlineSig:
                        xILOpCode = new ILOpCodes.OpSig(xOpCodeVal, xOpPos, xPos + 4, ReadInt32(il, xPos), xCurrentExceptionRegion);
                        xPos = xPos + 4;
                        break;

                    case OperandType.InlineString:
                        xILOpCode = new ILOpCodes.OpString(xOpCodeVal, xOpPos, xPos + 4, module.ResolveString(ReadInt32(il, xPos)), xCurrentExceptionRegion);
                        xPos = xPos + 4;
                        break;

                    case OperandType.InlineSwitch:
                        {
                            int xCount = ReadInt32(il, xPos);
                            xPos = xPos + 4;
                            int xNextOpPos = xPos + xCount * 4;
                            var xBranchLocations = new int[xCount];
                            for (int i = 0; i < xCount; i++)
                            {
                                xBranchLocations[i] = xNextOpPos + ReadInt32(il, xPos + i * 4);
                                CheckBranch(xBranchLocations[i], il.Length);
                            }
                            xILOpCode = new ILOpCodes.OpSwitch(xOpCodeVal, xOpPos, xNextOpPos, xBranchLocations, xCurrentExceptionRegion);
                            xPos = xNextOpPos;
                            break;
                        }

                    // The operand is a FieldRef, MethodRef, or TypeRef token.
                    case OperandType.InlineTok:
                        xILOpCode = new ILOpCodes.OpToken(xOpCodeVal, xOpPos, xPos + 4, ReadInt32(il, xPos), module, typeGenArgs, methodGenArgs, xCurrentExceptionRegion);
                        xPos = xPos + 4;
                        break;

                    // 32-bit metadata token.
                    case OperandType.InlineType:
                        {
                            var xValue = module.ResolveType(ReadInt32(il, xPos), typeGenArgs, methodGenArgs);
                            xILOpCode = new ILOpCodes.OpType(xOpCodeVal, xOpPos, xPos + 4, xValue, xCurrentExceptionRegion);
                            xPos = xPos + 4;
                            break;
                        }

                    case OperandType.ShortInlineVar:
                        switch (xOpCodeVal)
                        {
                            case ILOpCode.Code.Ldloc_S:
                                xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Ldloc, xOpPos, xPos + 1, il[xPos], xCurrentExceptionRegion);
                                break;
                            case ILOpCode.Code.Ldloca_S:
                                xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Ldloca, xOpPos, xPos + 1, il[xPos], xCurrentExceptionRegion);
                                break;
                            case ILOpCode.Code.Ldarg_S:
                                xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Ldarg, xOpPos, xPos + 1, il[xPos], xCurrentExceptionRegion);
                                break;
                            case ILOpCode.Code.Ldarga_S:
                                xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Ldarga, xOpPos, xPos + 1, il[xPos], xCurrentExceptionRegion);
                                break;
                            case ILOpCode.Code.Starg_S:
                                xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Starg, xOpPos, xPos + 1, il[xPos], xCurrentExceptionRegion);
                                break;
                            case ILOpCode.Code.Stloc_S:
                                xILOpCode = new ILOpCodes.OpVar(ILOpCode.Code.Stloc, xOpPos, xPos + 1, il[xPos], xCurrentExceptionRegion);
                                break;
                            default:
                                xILOpCode = new ILOpCodes.OpVar(xOpCodeVal, xOpPos, xPos + 1, il[xPos], xCurrentExceptionRegion);
                                break;
                        }
                        xPos = xPos + 1;
                        break;
                    case OperandType.InlineVar:
                        xILOpCode = new ILOpCodes.OpVar(xOpCodeVal, xOpPos, xPos + 2, ReadUInt16(il, xPos), xCurrentExceptionRegion);
                        xPos = xPos + 2;
                        break;

                    default:
                        throw new Exception("Unknown OperandType");
                }
                xILOpCode.InitStackAnalysis(method);
                result.Add(xILOpCode);
            }

            return result.ToImmutableArray();
        }

        // We could use BitConvertor, unfortunately they "hard coded" endianness. Its fine for reading IL now...
        // but they essentially do the same as we do, just a bit slower.
        private ushort ReadUInt16(byte[] aBytes, int aPos)
        {
            return (ushort)(aBytes[aPos + 1] << 8 | aBytes[aPos]);
        }

        private int ReadInt32(byte[] aBytes, int aPos)
        {
            return aBytes[aPos + 3] << 24 | aBytes[aPos + 2] << 16 | aBytes[aPos + 1] << 8 | aBytes[aPos];
        }

        private ulong ReadUInt64(byte[] aBytes, int aPos)
        {
            //return (UInt64)(
            //  aBytes[aPos + 7] << 56 | aBytes[aPos + 6] << 48 | aBytes[aPos + 5] << 40 | aBytes[aPos + 4] << 32
            //  | aBytes[aPos + 3] << 24 | aBytes[aPos + 2] << 16 | aBytes[aPos + 1] << 8 | aBytes[aPos]);

            return BitConverter.ToUInt64(aBytes, aPos);
        }

    }
}
