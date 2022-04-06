namespace IL2Amiga.Engine.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class InlineAttribute : Attribute
    {
        /// <summary>
        /// This field currently does nothing, but is here for later use.
        /// </summary>
        public TargetPlatform TargetPlatform = TargetPlatform.M68k;
    }

    /// <summary>
    /// This enum contains the possible target platforms,
    /// to eventually allow for selective inclusion of plugs,
    /// depending on the target platform.
    /// </summary>
    public enum TargetPlatform
    {
        x86,
        x64,
        IA64,
        ARM,
        M68k,
    }
}
