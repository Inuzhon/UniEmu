namespace UniEmu.Scripting.Api;

/// <summary>
/// Тип значения тега, доступного в скрипте.
/// </summary>
public enum TagScriptValueType
{
    /// <summary>
    /// Логическое значение.
    /// </summary>
    Bool,

    /// <summary>
    /// Целочисленное значение.
    /// </summary>
    Int,

    /// <summary>
    /// Число с плавающей точкой.
    /// </summary>
    Double,

    /// <summary>
    /// Строковое значение.
    /// </summary>
    String,
}
