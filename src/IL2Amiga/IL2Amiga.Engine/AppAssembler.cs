using System.Collections.Immutable;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace IL2Amiga.Engine
{
    public class AppAssembler
    {
        readonly ILogger<AppAssembler> logger;
        public AppAssembler(ILogger<AppAssembler> logger)
        {
            this.logger = logger;
        }
        public void EmitEntrypoint(MethodBase entrypoint) => EmitEntrypoint(entrypoint, ImmutableArray<MethodBase>.Empty);
        public void EmitEntrypoint(MethodBase? entrypoint, ImmutableArray<MethodBase> bootEntries)
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
