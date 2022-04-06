namespace IL2Amiga.Engine.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public sealed class OpCodeAttribute : Attribute
    {
        public ILOpCode.Code OpCode { get; }

        public OpCodeAttribute(ILOpCode.Code opCode)
        {
            OpCode = opCode;
        }
    }
}
