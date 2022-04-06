using System.Reflection;

namespace IL2Amiga.Engine.Extensions
{
    public static class FieldExtensions
    {
        public static string GetFullName(this FieldInfo field)
        {
            return $"{field.FieldType.GetFullName()} {field.DeclaringType!.GetFullName()}.{field.Name}";
        }
    }
}
