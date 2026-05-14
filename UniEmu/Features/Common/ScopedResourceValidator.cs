using Microsoft.EntityFrameworkCore;
using UniEmu.Contracts.Enums;
using UniEmu.Data;

namespace UniEmu.Features.Common;

/// <summary>
/// Validates resources that can be either shared or bound to a single emulator.
/// </summary>
public sealed class ScopedResourceValidator(UniEmuDbContext db)
{
    public Task<bool> IsValidScriptScopeAsync(
        ScriptScope scope,
        string? emulatorId,
        CancellationToken cancellationToken)
    {
        return IsValidAsync(
            scope == ScriptScope.Shared,
            scope == ScriptScope.Emulator,
            emulatorId,
            cancellationToken);
    }

    public Task<bool> IsValidCncScopeAsync(
        CncScope scope,
        string? emulatorId,
        CancellationToken cancellationToken)
    {
        return IsValidAsync(
            scope == CncScope.Shared,
            scope == CncScope.Emulator,
            emulatorId,
            cancellationToken);
    }

    private async Task<bool> IsValidAsync(
        bool isShared,
        bool isEmulatorScoped,
        string? emulatorId,
        CancellationToken cancellationToken)
    {
        if (isShared)
        {
            return string.IsNullOrWhiteSpace(emulatorId);
        }

        if (!isEmulatorScoped || string.IsNullOrWhiteSpace(emulatorId))
        {
            return false;
        }

        return await db.Emulators.AnyAsync(e => e.Id == emulatorId, cancellationToken);
    }
}
