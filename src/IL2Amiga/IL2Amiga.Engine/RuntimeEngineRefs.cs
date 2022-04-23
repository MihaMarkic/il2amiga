using System.Reflection;
using Cosmos.IL2CPU;

namespace IL2Amiga.Engine
{
    public class RuntimeEngineRefs {
        //public static readonly Assembly? RuntimeAssemblyDef;
        public MethodBase FinalizeApplicationRef { get; }
        public MethodBase InitializeApplicationRef { get; }
        public MethodBase Heap_AllocNewObjectRef { get; }

        public RuntimeEngineRefs()
        {
            Type engineType = typeof(RuntimeEngine);
            FinalizeApplicationRef = GetRequiredMethod(engineType, nameof(RuntimeEngine.FinalizeApplication));
            InitializeApplicationRef = GetRequiredMethod(engineType, nameof(RuntimeEngine.InitializeApplication));
            Heap_AllocNewObjectRef = GetRequiredMethod(engineType, nameof(RuntimeEngine.Heap_AllocNewObject));
        }

        internal static MethodInfo GetRequiredMethod(Type engineType, string name)
        {
            return engineType.GetMethod(name) ?? throw new Exception($"Couldn't find RuntimeEngine.{name} method");
        }
    }
}
