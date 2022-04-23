using System.Reflection;

namespace IL2Amiga.Engine
{
    //public class VTablesImplRefs
    //{
    //    public readonly Assembly? RuntimeAssemblyDef;
    //    public readonly Type? VTablesImplDef;
    //    public readonly MethodBase? SetTypeInfoRef;
    //    public readonly MethodBase? SetInterfaceInfoRef;
    //    public readonly MethodBase? SetMethodInfoRef;
    //    public readonly MethodBase? SetInterfaceMethodInfoRef;
    //    public readonly MethodBase? GetMethodAddressForTypeRef;
    //    public readonly MethodBase? GetMethodAddressForInterfaceTypeRef;
    //    public readonly MethodBase? GetDeclaringTypeOfMethodForTypeRef;
    //    public readonly MethodBase? IsInstanceRef;
    //    public readonly MethodBase? GetBaseTypeRef;

    //    public Func<Type, uint>? GetTypeId;
    //    // GC Methods
    //    public readonly MethodBase? GetGCFieldCount;

    //    public VTablesImplRefs(TypeResolver typeResolver)
    //    {
    //        VTablesImplDef = typeResolver.ResolveType("Cosmos.Core.VTablesImpl, Cosmos.Core", true);
    //        if (VTablesImplDef == null)
    //        {
    //            throw new Exception("Cannot find VTablesImpl in Cosmos.Core!");
    //        }
    //        foreach (FieldInfo xField in typeof(VTablesImplRefs).GetFields())
    //        {
    //            if (xField.Name.EndsWith("Ref"))
    //            {
    //                string methodName = xField.Name.Substring(0, xField.Name.Length - "Ref".Length);
    //                MethodBase? tempMethod = VTablesImplDef.GetMethod(methodName);
    //                if (tempMethod is null)
    //                {
    //                    throw new Exception($"Method '{methodName}' not found on VTablesImpl!");
    //                }
    //                xField.SetValue(null, tempMethod);
    //            }
    //        }
    //    }
    //}
}
