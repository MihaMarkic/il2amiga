﻿using System.Reflection;
using IL2Amiga.Engine.MethodAnalysis;

namespace IL2Amiga.Engine.ILOpCodes
{
    public class OpDouble : ILOpCode
    {
        public double Value { get; }

        public OpDouble(Code aOpCode, int aPos, int aNextPos, double aValue, ExceptionRegionInfoEx? aCurrentExceptionRegion)
          : base(aOpCode, aPos, aNextPos, aCurrentExceptionRegion)
        {
            Value = aValue;
        }

        public override int GetNumberOfStackPops(MethodBase aMethod)
        {
            switch (OpCode)
            {
                case Code.Ldc_R8:
                    return 0;
                default:
                    throw new NotImplementedException($"OpCode '{OpCode}' not implemented!");
            }
        }

        public override int GetNumberOfStackPushes(MethodBase aMethod)
        {
            switch (OpCode)
            {
                case Code.Ldc_R8:
                    return 1;
                default:
                    throw new NotImplementedException($"OpCode '{OpCode}' not implemented!");
            }
        }

        protected override void DoInitStackAnalysis(MethodBase aMethod)
        {
            base.DoInitStackAnalysis(aMethod);

            switch (OpCode)
            {
                case Code.Ldc_R8:
                    StackPushTypes[0] = typeof(double);
                    break;
                default:
                    break;
            }
        }

        public override void DoInterpretStackTypes()
        {

        }
    }
}
