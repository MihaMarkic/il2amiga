using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;

namespace IL2Amiga.Engine
{
    internal class IsolatedAssemblyLoadContext : AssemblyLoadContext
    {
        ImmutableDictionary<AssemblyIdentity, Lazy<Assembly>> assemblies;

        public IsolatedAssemblyLoadContext(IEnumerable<string> assemblyPaths)
        {
            var tempAssemblies = new Dictionary<AssemblyIdentity, Lazy<Assembly>>();

            foreach (var assemblyPath in assemblyPaths)
            {
                AssemblyName assemblyName;

                try
                {
                    assemblyName = GetAssemblyName(assemblyPath);
                }
                catch (ArgumentException e)
                {
                    throw new FileLoadException($"Failed to get assembly name for '{assemblyPath}' !", e);
                }

                var assemblyIdentity = new AssemblyIdentity(assemblyName);

                if (tempAssemblies.ContainsKey(assemblyIdentity))
                {
                    continue;
                }
                else
                {
                    // HACK: need to fix assembly loading
                    if (!AppDomain.CurrentDomain.GetAssemblies().Any(
                        a => new AssemblyIdentity(a.GetName()).Equals(assemblyIdentity)))
                    {
                        Default.LoadFromAssemblyPath(assemblyPath);
                    }

                    tempAssemblies.Add(
                        assemblyIdentity,
                        new Lazy<Assembly>(
                            () =>
                            {
                                // HACK: need to fix assembly loading
                                return Default.LoadFromAssemblyPath(assemblyPath);

                                //return LoadFromAssemblyPath(assemblyPath);
                            }));
                }
            }
            assemblies = tempAssemblies.ToImmutableDictionary();
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var assemblyIdentity = new AssemblyIdentity(assemblyName);
            assemblies.TryGetValue(assemblyIdentity, out var assembly);

            return assembly?.Value;
        }
    }
}
