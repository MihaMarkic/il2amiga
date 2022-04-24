using System.Collections.Immutable;
using System.Reflection;
using IL2Amiga.Engine.Attributes;

namespace IL2Amiga.Engine
{
    public class Il2cpuMethodInfo
    {
        public enum TypeEnum { Normal, Plug, NeedsPlug };
        public MethodBase MethodBase { get; }
        public TypeEnum Type { get; }
        //TODO: Figure out if we really need three different ids
        public uint UID { get; }
        public long DebugMethodUID { get; set; }
        public long DebugMethodLabelUID { get; set; }
        public long EndMethodID { get; set; }
        public string MethodLabel { get; private set; }
        /// <summary>
        /// The method info for the method which plugs this one
        /// </summary>
        public Il2cpuMethodInfo? PlugMethod { get; }
        public Type? MethodAssembler { get; }
        public bool IsInlineAssembler { get; }
        public bool DebugStubOff { get; }

        Il2cpuMethodInfo? pluggedMethod;
        /// <summary>
        /// Method which is plugged by this method
        /// </summary>
        public Il2cpuMethodInfo? PluggedMethod
        {
            get => pluggedMethod; set
            {
                pluggedMethod = value;
                if (PluggedMethod != null)
                {
                    MethodLabel = "PLUG_FOR___" + LabelName.Get(PluggedMethod.MethodBase);
                }
                else
                {
                    MethodLabel = LabelName.Get(MethodBase);
                }
            }
        }
        public uint LocalVariablesSize { get; set; }

        public bool IsWildcard { get; set; }

        public Il2cpuMethodInfo(MethodBase methodBase, uint uID, TypeEnum type, Il2cpuMethodInfo? plugMethod, Type? methodAssembler) 
            : this(methodBase, uID, type, plugMethod, false)
        {
            MethodAssembler = methodAssembler;
        }


        public Il2cpuMethodInfo(MethodBase methodBase, uint uID, TypeEnum type, Il2cpuMethodInfo? plugMethod)
            : this(methodBase, uID, type, plugMethod, false)
        {
        }

        public Il2cpuMethodInfo(MethodBase methodBase, uint uID, TypeEnum type, Il2cpuMethodInfo? plugMethod, bool isInlineAssembler)
        {
            MethodBase = methodBase;
            UID = uID;
            Type = type;
            PlugMethod = plugMethod;
            IsInlineAssembler = isInlineAssembler;

            var attributes = methodBase.GetCustomAttributes<DebugStub>(false).ToImmutableArray();
            if (attributes.Length > 0)
            {
                var attrib = new DebugStub
                {
                    Off = attributes[0].Off,
                };
                DebugStubOff = attrib.Off;
            }

            MethodLabel = LabelName.Get(MethodBase);
        }
    }
}
