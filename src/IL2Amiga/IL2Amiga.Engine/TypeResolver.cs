using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace IL2Amiga.Engine
{
    public class TypeResolver
    {
        readonly ILogger<TypeResolver> logger;
        readonly AssemblyLoadContext assemblyLoadContext;
        public TypeResolver(ILogger<TypeResolver> logger, IsolatedAssemblyLoadContext assemblyLoadContext)
        {
            this.logger = logger;
            this.assemblyLoadContext = assemblyLoadContext;
        }
        public Type? ResolveType(string typeName) => Type.GetType(typeName, ResolveAssembly, ResolveType);
        public Type? ResolveType(string typeName, bool throwOnError) =>
            Type.GetType(typeName, ResolveAssembly, ResolveType, throwOnError);
        public Type? ResolveType(string typeName, bool throwOnError, bool ignoreCase) =>
            Type.GetType(typeName, ResolveAssembly, ResolveType, throwOnError, ignoreCase);
        Assembly ResolveAssembly(AssemblyName assemblyName) =>
            assemblyLoadContext.LoadFromAssemblyName(assemblyName);
        Type? ResolveType(Assembly? assembly, string typeName, bool ignoreCase) =>
            assembly?.GetType(typeName, false, ignoreCase);
    }
}
