﻿using System;
using System.Reflection;
using IL2Amiga.Engine.MethodAnalysis;

namespace IL2Amiga.Engine.ILOpCodes
{
    public class OpString : ILOpCode
    {
        public string Value { get; }

        public OpString(Code aOpCode, int aPos, int aNextPos, string aValue, ExceptionRegionInfoEx? aCurrentExceptionRegion)
          : base(aOpCode, aPos, aNextPos, aCurrentExceptionRegion)
        {
            Value = aValue;
        }

        public override int GetNumberOfStackPops(MethodBase aMethod)
        {
            switch (OpCode)
            {
                case Code.Ldstr:
                    return 0;
                default:
                    throw new NotImplementedException("OpCode '" + OpCode + "' not implemented!");
            }
        }

        public override int GetNumberOfStackPushes(MethodBase aMethod)
        {
            switch (OpCode)
            {
                case Code.Ldstr:
                    return 1;
                default:
                    throw new NotImplementedException("OpCode '" + OpCode + "' not implemented!");
            }
        }

        protected override void DoInitStackAnalysis(MethodBase aMethod)
        {
            base.DoInitStackAnalysis(aMethod);

            switch (OpCode)
            {
                case Code.Ldstr:
                    StackPushTypes![0] = typeof(string);
                    break;
                default:
                    break;
            }
        }

        public override void DoInterpretStackTypes() { }
    }
}
