namespace IL2Amiga.Engine.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class Plug : Attribute
    {
        public Plug()
        {
        }

        public Plug(Type target)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public Plug(string targetName)
        {
            if (string.IsNullOrEmpty(targetName))
            {
                throw new ArgumentNullException(nameof(targetName));
            }

            TargetName = targetName;
        }

        public Type? Target { get; set; }

        /// <summary>
        /// TargetName can be used to load private/internal classes. It has the format "[Class name], [Assembly]"
        /// </summary>
        public string? TargetName { get; set; }

        public bool IsOptional { get; set; }

        public bool Inheritable { get; set; }
    }
}
