namespace UniEmu.Runtime.Models;

public sealed class TagScriptValue(object? value)
{
    public object? Value { get; } = value;

    public double AsDouble(double fallback = 0)
    {
        return TelemetryValueGenerator.ToNumericValue(Value) ?? fallback;
    }

    public int AsInt(int fallback = 0) => (int)Math.Round(AsDouble(fallback));

    public bool AsBool(bool fallback = false)
    {
        return Value switch
        {
            bool boolValue => boolValue,
            string stringValue when bool.TryParse(stringValue, out var boolValue) => boolValue,
            null => fallback,
            _ => AsDouble(fallback ? 1 : 0) != 0,
        };
    }

    public string AsString(string fallback = "") => Value?.ToString() ?? fallback;

    public override string ToString() => AsString();
}
