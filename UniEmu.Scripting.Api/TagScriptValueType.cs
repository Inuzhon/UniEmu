namespace UniEmu.Scripting.Api;

/// <summary>
/// Тип значения тега, доступного в скрипте.
/// </summary>
[ScriptingApi]
public enum TagScriptValueType
{
    /// <summary>
    /// Логическое значение.
    /// </summary>
    [ScriptingApi]
    Bool,

    /// <summary>
    /// Целочисленное значение.
    /// </summary>
    [ScriptingApi]
    Int,

    /// <summary>
    /// Число с плавающей точкой.
    /// </summary>
    [ScriptingApi]
    Double,

    /// <summary>
    /// Строковое значение.
    /// </summary>
    [ScriptingApi]
    String,
}
