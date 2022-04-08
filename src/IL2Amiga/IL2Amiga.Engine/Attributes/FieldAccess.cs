namespace IL2Amiga.Engine.Attributes
{
    [AttributeUsage(AttributeTargets.Parameter)]
	public sealed class FieldAccess : Attribute
	{
		public string? Name { get; init; }
	}
}
