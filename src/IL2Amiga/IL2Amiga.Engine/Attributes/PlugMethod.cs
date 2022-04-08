namespace IL2Amiga.Engine.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class PlugMethod : Attribute
    {
        public string? Signature { get; init; }
        public bool Enabled { get; init; } = true;
        public Type? Assembler { get; init; }
        public bool PlugRequired { get; init; }
        public bool IsWildcard { get; init; }
        public bool WildcardMatchParameters { get; init; }
        public bool IsOptional { get; init; } = true;
    }
}
