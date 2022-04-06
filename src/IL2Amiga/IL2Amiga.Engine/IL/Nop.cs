using IL2Amiga.Engine.Attributes;

namespace IL2Amiga.Engine.IL
{
    [OpCode(ILOpCode.Code.Nop)]
    public class Nop : ILOp
    {
        public override void Execute(Il2cpuMethodInfo aMethod, ILOpCode aOpCode)
        {
            //XS.Noop();
        }

    }
}
