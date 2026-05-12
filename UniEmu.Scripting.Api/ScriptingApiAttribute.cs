namespace UniEmu.Scripting.Api;

/// <summary>
/// Marks a type or member as visible to user scripts in the UniEmu scripting API.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class
    | AttributeTargets.Struct
    | AttributeTargets.Enum
    | AttributeTargets.Interface
    | AttributeTargets.Constructor
    | AttributeTargets.Method
    | AttributeTargets.Property
    | AttributeTargets.Field,
    Inherited = false)]
public sealed class ScriptingApiAttribute : Attribute;
