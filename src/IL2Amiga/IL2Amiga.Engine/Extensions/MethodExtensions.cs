using System.Reflection;
using IL2Amiga.Engine.MethodAnalysis;
using IL2CPU.Debug.Symbols;

namespace IL2Amiga.Engine.Extensions
{
    internal static class MethodExtensions
    {
        public static string GetFullName(this MethodBase method)
        {
            return LabelName.Get(method);
        }

        public static IList<LocalVariableInfo> GetLocalVariables(this MethodBase source)
        {
            return DebugSymbolReader.GetLocalVariableInfos(source);
        }

        public static IEnumerable<ExceptionRegionInfoEx> GetExceptionRegionInfos(this MethodBase source)
        {
            var clauses = source.GetMethodBody()?.ExceptionHandlingClauses;
            if (clauses is not null)
            {
                foreach (var x in clauses)
                {
                    yield return new ExceptionRegionInfoEx(x);
                }
            }
        }
    }
}
