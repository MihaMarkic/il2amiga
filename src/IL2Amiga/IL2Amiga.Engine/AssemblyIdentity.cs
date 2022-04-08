using System.Reflection;

namespace IL2Amiga.Engine
{
    internal class AssemblyIdentity : IEquatable<AssemblyIdentity>
    {
        readonly AssemblyName assemblyName;
        public AssemblyIdentity(AssemblyName assemblyName)
        {
            this.assemblyName = assemblyName;
        }
        public bool Equals(AssemblyIdentity? other) => assemblyName.Name == other?.assemblyName.Name;
        public override bool Equals(object? obj) => obj is AssemblyIdentity other && Equals(other);
        public override int GetHashCode() => assemblyName.Name?.GetHashCode() ?? throw new NullReferenceException();
        public override string ToString() => assemblyName.Name ?? throw new NullReferenceException();
    }
}
