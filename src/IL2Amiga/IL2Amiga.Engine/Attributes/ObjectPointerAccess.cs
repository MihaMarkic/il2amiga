namespace IL2Amiga.Engine.Attributes
{
    /// <summary>
    /// This attribute is used on plug parameters, that need the unsafe pointer to an object's data area
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class ObjectPointerAccess : Attribute
    {

    }
}
