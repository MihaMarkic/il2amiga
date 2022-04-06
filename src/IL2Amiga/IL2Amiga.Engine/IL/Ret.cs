using IL2Amiga.Engine.Attributes;

namespace IL2Amiga.Engine.IL
{
    [OpCode(ILOpCode.Code.Ret)]
    public class Ret : ILOp
    {
        public override void Execute(Il2cpuMethodInfo aMethod, ILOpCode aOpCode)
        {
            //TODO: Return
            Jump_End(aMethod);
            // Need to jump to end of method. Assembler can emit this label for now
            //XS.Jump(MethodFooterOp.EndOfMethodLabelNameNormal);
        }
    }
}
