namespace UniEmu.Scripting.Api;

/// <summary>
/// Помечает тип или член как доступный пользовательским скриптам UniEmu.
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
