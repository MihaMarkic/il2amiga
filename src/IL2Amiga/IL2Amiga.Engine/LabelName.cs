using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace IL2Amiga.Engine
{
    public static class LabelName
    {
        /// <summary>
        /// Cache for label names.
        /// </summary>
        private static Dictionary<MethodBase, string> labelNamesCache = new Dictionary<MethodBase, string>();

        // All label naming code should be changed to use this class.

        // Label bases can be up to 200 chars. If larger they will be shortened with an included hash.
        // This leaves up to 56 chars for suffix information.

        // Suffixes are a series of tags and have their own prefixes to preserve backwards compat.
        // .GUID_xxxxxx
        // .IL_0000
        // .ASM_00 - future, currently is IL_0000 or IL_0000.00
        // Would be nice to combine IL and ASM into IL_0000_00, but the way we work with the assembler currently
        // we cant because the ASM labels are issued as local labels.
        //
        // - Methods use a variety of alphanumeric suffixes for support code.
        // - .00 - asm markers at beginning of method
        // - .0000.00 IL.ASM marker

        public static int LabelCount { get; private set; }
        // Max length of labels at 256. We use lower here so that we still have room for suffixes for IL positions, etc.
        const int MaxLengthWithoutSuffix = 200;

        public static string Get(MethodBase aMethod)
        {
            if (labelNamesCache.TryGetValue(aMethod, out var result))
            {
                return result;
            }

            result = Final(GetFullName(aMethod));
            labelNamesCache.Add(aMethod, result);
            return result;
        }

        public static string Get(string aMethodLabel, int aIlPos)
        {
            return aMethodLabel + ".IL_" + aIlPos.ToString("X4");
        }

        private const string IllegalIdentifierChars = "&.,+$<>{}-`\'/\\ ()[]*!=";

        public static string FilterStringForIncorrectChars(string aName)
        {
            string xTempResult = aName;
            foreach (char c in IllegalIdentifierChars)
            {
                xTempResult = xTempResult.Replace(c, '_');
            }
            return xTempResult;
        }

        // no array bracket, they need to replace, for unique names for used types in methods
        private static readonly System.Text.RegularExpressions.Regex IllegalCharsReplace = new System.Text.RegularExpressions.Regex(@"[&.,+$<>{}\-\`\\'/\\ \(\)\*!=]", System.Text.RegularExpressions.RegexOptions.Compiled);

        public static string Final(string name)
        {
            //var xSB = new StringBuilder(xName);

            // DataMember.FilterStringForIncorrectChars also does some filtering but replacing empties or non _ chars
            // causes issues with legacy hard coded values. So we have a separate function.
            //
            // For logging possibilities, we generate fuller names, and then strip out spacing/characters.
            /*const string xIllegalChars = "&.,+$<>{}-`\'/\\ ()[]*!=_";
            foreach (char c in xIllegalChars) {
              xSB.Replace(c.ToString(), "");
            }*/
            name = name.Replace("[]", "array");
            name = name.Replace("<>", "compilergenerated");
            name = name.Replace("[,]", "array");
            name = name.Replace("*", "pointer");
            name = name.Replace("|", "sLine");

            name = IllegalCharsReplace.Replace(name, string.Empty);

            if (name.Length > MaxLengthWithoutSuffix)
            {
                using (var xHash = MD5.Create())
                {
                    var xValue = xHash.ComputeHash(Encoding.GetEncoding(0).GetBytes(name));
                    var xSB = new StringBuilder(name);
                    // Keep length max same as before.
                    xSB.Length = MaxLengthWithoutSuffix - xValue.Length * 2;
                    foreach (var xByte in xValue)
                    {
                        xSB.Append(xByte.ToString("X2"));
                    }
                    name = xSB.ToString();
                }
            }

            LabelCount++;
            return name;
        }

        public static string GetFullName(Type type)
        {
            if (type.IsGenericParameter)
            {
                return type.FullName ?? throw new NullReferenceException();
            }
            var sb = new StringBuilder(256);
            if (type.IsArray)
            {
                sb.Append(GetFullName(type.GetElementType() ?? throw new NullReferenceException()));
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
                return "&" + GetFullName(type.GetElementType() ?? throw new NullReferenceException());
            }
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                sb.Append(GetFullName(type.GetGenericTypeDefinition()));

                sb.Append("<");
                var xArgs = type.GetGenericArguments();
                for (int i = 0; i < xArgs.Length - 1; i++)
                {
                    sb.Append(GetFullName(xArgs[i]));
                    sb.Append(", ");
                }
                sb.Append(GetFullName(xArgs.Last()));
                sb.Append(">");
            }
            else
            {
                sb.Append(type.FullName);
            }

            if (type.Name == "SR" || type.Name == "PathInternal" || type.Name.Contains("PrivateImplementationDetails")) //TODO:  we need to deal with this more generally
            {
                return type.Assembly.FullName?.Split(',')[0].Replace(".", "") + sb.ToString();
            }

            if (type.Name == "Error" || type.Name == "GetEndOfFile")
            {
                return type.Assembly.FullName?.Split(',')[0].Replace(".", "") + sb.ToString();
            }

            return sb.ToString();
        }

        public static string GetFullName(MethodBase method)
        {
            if (method is null)
            {
                throw new ArgumentNullException(nameof(method));
            }
            var builder = new StringBuilder(256);
            var parts = method.ToString()?.Split(' ') ?? Array.Empty<string>();
            var parts2 = parts.Skip(1).ToArray();
            var methodInfo = method as MethodInfo;
            if (methodInfo is not null)
            {
                builder.Append(GetFullName(methodInfo.ReturnType));
            }
            else
            {
                var xCtor = method as ConstructorInfo;
                if (xCtor != null)
                {
                    builder.Append(typeof(void).FullName);
                }
                else
                {
                    builder.Append(parts[0]);
                }
            }
            builder.Append("  ");
            if (method.DeclaringType != null)
            {
                builder.Append(GetFullName(method.DeclaringType));
            }
            else
            {
                builder.Append("dynamic_method");
            }
            builder.Append(".");
            if (method.IsGenericMethod && !method.IsGenericMethodDefinition)
            {
                builder.Append(methodInfo?.GetGenericMethodDefinition().Name ?? throw new NullReferenceException());

                var xGenArgs = method.GetGenericArguments();
                if (xGenArgs.Length > 0)
                {
                    builder.Append("<");
                    for (int i = 0; i < xGenArgs.Length - 1; i++)
                    {
                        builder.Append(GetFullName(xGenArgs[i]));
                        builder.Append(", ");
                    }
                    builder.Append(GetFullName(xGenArgs.Last()));
                    builder.Append(">");
                }
            }
            else
            {
                builder.Append(method.Name);
            }
            builder.Append("(");
            var xParams = method.GetParameters();
            for (var i = 0; i < xParams.Length; i++)
            {
                if (i == 0 && xParams[i].Name == "aThis")
                {
                    continue;
                }
                builder.Append(GetFullName(xParams[i].ParameterType));
                if (i < (xParams.Length - 1))
                {
                    builder.Append(", ");
                }
            }
            builder.Append(")");
            return builder.ToString();
        }

        public static string GetFullName(FieldInfo aField)
        {
            return $"{GetFullName(aField.FieldType)} {GetFullName(aField.DeclaringType ?? throw new NullReferenceException())}.{aField.Name}";
        }

        public static string GetStaticFieldName(FieldInfo aField)
        {
            return FilterStringForIncorrectChars(
                $"static_field__{GetFullName(aField.DeclaringType ?? throw new NullReferenceException())}.{aField.Name}");
        }
    }
}
