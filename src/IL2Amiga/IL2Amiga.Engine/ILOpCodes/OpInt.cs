﻿using System.Reflection;
using IL2Amiga.Engine.MethodAnalysis;

namespace IL2Amiga.Engine.ILOpCodes
{
    public class OpInt : ILOpCode
    {
        public int Value { get; }

        public OpInt(Code aOpCode, int aPos, int aNextPos, int aValue, ExceptionRegionInfoEx? aCurrentExceptionRegion)
          : base(aOpCode, aPos, aNextPos, aCurrentExceptionRegion)
        {
            Value = aValue;
        }

        public override int GetNumberOfStackPops(MethodBase aMethod)
        {
            switch (OpCode)
            {
                case Code.Ldc_I4:
                    return 0;
                case Code.Unaligned:
                    return 0;
                default:
                    throw new NotImplementedException($"OpCode '{OpCode}' not implemented!");
            }
        }

        public override int GetNumberOfStackPushes(MethodBase aMethod)
        {
            switch (OpCode)
            {
                case Code.Ldc_I4:
                    return 1;
                case Code.Unaligned:
                    return 0;
                default:
                    throw new NotImplementedException($"OpCode '{OpCode}' not implemented!");
            }
        }

        protected override void DoInitStackAnalysis(MethodBase aMethod)
        {
            base.DoInitStackAnalysis(aMethod);

            switch (OpCode)
            {
                case Code.Ldc_I4:
                    StackPushTypes[0] = typeof(int);
                    return;
                default:
                    break;
            }
        }

        public override void DoInterpretStackTypes()
        {
        }
    }
}
