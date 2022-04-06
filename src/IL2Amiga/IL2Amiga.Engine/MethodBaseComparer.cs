using System.Reflection;
using IL2Amiga.Engine.Extensions;

namespace IL2Amiga.Engine
{
    public class MethodBaseComparer : IComparer<MethodBase>, IEqualityComparer<MethodBase>
    {
        public int Compare(MethodBase? x, MethodBase? y) =>
            string.Compare(x.GetFullName(), y.GetFullName(), StringComparison.Ordinal);

        public bool Equals(MethodBase? x, MethodBase? y) =>
            string.Equals(x.GetFullName(), y.GetFullName(), StringComparison.Ordinal);

        public int GetHashCode(MethodBase obj) => obj.GetFullName().GetHashCode();
    }
}
