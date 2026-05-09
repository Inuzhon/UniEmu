namespace UniEmu.Runtime.Scripting;

public sealed class CsxScriptValidationException(IReadOnlyList<CsxDiagnostic> diagnostics)
    : InvalidOperationException("CSX script validation failed.")
{
    public IReadOnlyList<CsxDiagnostic> Diagnostics { get; } = diagnostics;
}
