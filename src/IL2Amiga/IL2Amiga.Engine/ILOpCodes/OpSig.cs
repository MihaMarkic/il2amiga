using System.Reflection;
using IL2Amiga.Engine.MethodAnalysis;

namespace IL2Amiga.Engine.ILOpCodes
{
    public class OpSig : ILOpCode
    {
        public int Value { get; }

        public OpSig(Code aOpCode, int aPos, int aNextPos, int aValue, ExceptionRegionInfoEx? aCurrentExceptionRegion)
          : base(aOpCode, aPos, aNextPos, aCurrentExceptionRegion)
        {
            Value = aValue;
        }

        public override int GetNumberOfStackPops(MethodBase aMethod)
        {
            switch (OpCode)
            {
                default:
                    throw new NotImplementedException("OpCode '" + OpCode + "' not implemented!");
            }
        }

        public override int GetNumberOfStackPushes(MethodBase aMethod)
        {
            switch (OpCode)
            {
                default:
                    throw new NotImplementedException("OpCode '" + OpCode + "' not implemented!");
            }
        }

        public override void DoInterpretStackTypes() { }
    }
}
