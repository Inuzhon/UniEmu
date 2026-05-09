using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Mapping;
using UniEmu.Runtime;

namespace UniEmu.Features.Tags;

public sealed class TagService(
    UniEmuDbContext db,
    CachedUniEmuDataService dataCache,
    EmulatorScheduleService scheduleService)
{
    public async Task<IReadOnlyList<EmulatorTagDto>?> ListAsync(string emulatorId, CancellationToken cancellationToken)
    {
        if (!await db.Emulators.AnyAsync(e => e.Id == emulatorId, cancellationToken))
        {
            return null;
        }

        var tags = await db.EmulatorTags
            .AsNoTracking()
            .Where(t => t.EmulatorId == emulatorId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return tags.Select(t => t.ToDto()).ToList();
    }

    public async Task<EmulatorTagDto?> CreateAsync(string emulatorId, CreateTagRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Emulators.AnyAsync(e => e.Id == emulatorId, cancellationToken))
        {
            return null;
        }

        var entity = new EmulatorTagEntity
        {
            Id = $"tg-{Guid.NewGuid():N}"[..12],
            EmulatorId = emulatorId,
            Name = request.Name.Trim(),
            Key = request.Key.Trim(),
            Type = UniEmuJson.EnumString(request.Type),
            Source = UniEmuJson.EnumString(request.Source),
            Preview = request.Preview,
            TriggerJson = UniEmuJson.Serialize(request.Trigger),
            CalcJson = request.Calc is null ? null : UniEmuJson.Serialize(request.Calc),
            FormulaJson = request.Formula is null ? null : UniEmuJson.Serialize(request.Formula),
            ScenarioJson = request.Scenario is null ? null : UniEmuJson.Serialize(request.Scenario),
            SpecialParameter = request.SpecialParameter is null ? null : UniEmuJson.EnumString(request.SpecialParameter.Value),
            Description = request.Description,
        };

        db.EmulatorTags.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        dataCache.InvalidateEmulator(emulatorId);
        await scheduleService.RescheduleIfRunningAsync(emulatorId, cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmulatorTagDto?> ReplaceAsync(string emulatorId, string tagId, ReplaceTagRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.EmulatorTags
            .FirstOrDefaultAsync(t => t.EmulatorId == emulatorId && t.Id == tagId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.Name = request.Name.Trim();
        entity.Key = request.Key.Trim();
        entity.Type = UniEmuJson.EnumString(request.Type);
        entity.Source = UniEmuJson.EnumString(request.Source);
        entity.Preview = request.Preview;
        entity.TriggerJson = UniEmuJson.Serialize(request.Trigger);
        entity.CalcJson = request.Calc is null ? null : UniEmuJson.Serialize(request.Calc);
        entity.FormulaJson = request.Formula is null ? null : UniEmuJson.Serialize(request.Formula);
        entity.ScenarioJson = request.Scenario is null ? null : UniEmuJson.Serialize(request.Scenario);
        entity.SpecialParameter = request.SpecialParameter is null ? null : UniEmuJson.EnumString(request.SpecialParameter.Value);
        entity.Description = request.Description;

        await db.SaveChangesAsync(cancellationToken);
        dataCache.InvalidateEmulator(emulatorId);
        await scheduleService.RescheduleIfRunningAsync(emulatorId, cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(string emulatorId, string tagId, CancellationToken cancellationToken)
    {
        var deleted = await db.EmulatorTags
            .Where(t => t.EmulatorId == emulatorId && t.Id == tagId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            dataCache.InvalidateEmulator(emulatorId);
            await scheduleService.RescheduleIfRunningAsync(emulatorId, cancellationToken);
        }

        return deleted > 0;
    }
}
