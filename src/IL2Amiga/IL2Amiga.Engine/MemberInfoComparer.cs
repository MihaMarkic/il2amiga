using System.Reflection;

namespace IL2Amiga.Engine
{
    internal class MemberInfoComparer : IEqualityComparer<MemberInfo>
    {
        public static MemberInfoComparer Instance { get; } = new MemberInfoComparer();

        public bool Equals(MemberInfo? x, MemberInfo? y)
        {
            if (x is null)
            {
                return y is null;
            }

            if (y is null)
            {
                return false;
            }

            if (x.GetType() == y.GetType())
            {
                if (x.MetadataToken == y.MetadataToken && x.Module == y.Module)
                {
                    if (x is MethodBase xMethod && y is MethodBase yMethod)
                    {
                        return LabelName.GetFullName(xMethod) == LabelName.GetFullName(yMethod);
                    }
                    else if (x is Type xType && y is Type yType)
                    {
                        return LabelName.GetFullName(xType) == LabelName.GetFullName(yType);
                    }
                    else
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public int GetHashCode(MemberInfo item)
        {
            return (item.ToString() + GetDeclareTypeString(item)).GetHashCode();
        }

        private static string GetDeclareTypeString(MemberInfo item)
        {
            var xName = item.DeclaringType;
            return xName is null ? string.Empty : xName.ToString();
        }
    }
}
