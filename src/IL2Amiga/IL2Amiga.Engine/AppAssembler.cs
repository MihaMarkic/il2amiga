using System.Collections.Immutable;
using System.Reflection;

namespace IL2Amiga.Engine
{
    public class AppAssembler
    {
        public void EmitEntrypoint(MethodBase entrypoint) => EmitEntrypoint(entrypoint, ImmutableArray<MethodBase>.Empty);
        public void EmitEntrypoint(MethodBase entrypoint, ImmutableArray<MethodBase> bootEntries)
        {
        }
        public void ProcessField(FieldInfo field)
        {
        }

        public void GenerateVMTCode(HashSet<Type> typesSet, HashSet<MethodBase> methodsSet, Func<Type, uint> getTypeID, Func<MethodBase, uint> getMethodUID)
        {

        }
    }
}
