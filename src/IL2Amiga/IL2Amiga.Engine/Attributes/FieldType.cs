namespace IL2Amiga.Engine.Attributes
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class FieldType : Attribute
    {
        public string? Name { get; init; }
    }
}
