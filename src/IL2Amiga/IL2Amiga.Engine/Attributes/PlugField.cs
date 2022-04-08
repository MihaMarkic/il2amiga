namespace IL2Amiga.Engine.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public sealed class PlugField : Attribute
	{
		public PlugField()
		{
		}

		public string? FieldId
		{
			get;
			set;
		}

		public bool IsExternalValue
		{
			get;
			set;
		}

		public Type? FieldType
		{
			get;
			set;
		}

	}
}
