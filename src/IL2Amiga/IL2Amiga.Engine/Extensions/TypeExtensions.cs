using System.Text;

namespace IL2Amiga.Engine.Extensions
{
    public static class TypeExtensions
    {
        public static string GetFullName(this Type type)
        {
            if (type.IsGenericParameter)
            {
                return type.Name;
            }
            var sb = new StringBuilder();
            if (type.IsArray)
            {
                sb.Append(type.GetElementType()!.GetFullName());
                sb.Append("[");
                int xRank = type.GetArrayRank();
                while (xRank > 1)
                {
                    sb.Append(",");
                    xRank--;
                }
                sb.Append("]");
                return sb.ToString();
            }
            if (type.IsByRef && type.HasElementType)
            {
                return "&" + type.GetElementType()!.GetFullName();
            }
            if (type.IsGenericType)
            {
                sb.Append(type.GetGenericTypeDefinition().FullName);
            }
            else
            {
                sb.Append(type.FullName);
            }
            if (type.ContainsGenericParameters)
            {
                sb.Append("<");
                var xArgs = type.GetGenericArguments();
                for (int i = 0; i < xArgs.Length - 1; i++)
                {
                    sb.Append(GetFullName(xArgs[i]));
                    sb.Append(", ");
                }
                if (xArgs.Length == 0)
                {
                    Console.Write("");
                }
                sb.Append(GetFullName(xArgs.Last()));
                sb.Append(">");
            }
            return sb.ToString();
        }
    }
}
