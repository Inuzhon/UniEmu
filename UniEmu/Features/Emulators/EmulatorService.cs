using Microsoft.EntityFrameworkCore;
using UniEmu.Common;
using UniEmu.Contracts.Dtos;
using UniEmu.Contracts.Enums;
using UniEmu.Contracts.Requests;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Mapping;
using UniEmu.Realtime;
using UniEmu.Runtime;

namespace UniEmu.Features.Emulators;

/// <summary>
/// Выполняет прикладные операции с эмуляторами и их расписанием.
/// </summary>
public sealed class EmulatorService(
    UniEmuDbContext db,
    CachedUniEmuDataService dataCache,
    EmulatorScheduleService scheduleService,
    RuntimeUpdateService runtimeUpdateService)
{
    /// <summary>
    /// Возвращает все эмуляторы с краткой статистикой.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Список эмуляторов.</returns>
    public async Task<IReadOnlyList<EmulatorDto>> ListAsync(CancellationToken cancellationToken)
    {
        var emulators = await db.Emulators
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .ThenBy(e => e.Status)
            .Select(e => new
            {
                Entity = e,
                TagsCount = e.Tags.Count,
            })
            .ToListAsync(cancellationToken);

        return emulators.Select(e => e.Entity.ToDto(e.TagsCount)).ToList();
    }

    /// <summary>
    /// Возвращает эмулятор по идентификатору.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Эмулятор или <see langword="null"/>, если он не найден.</returns>
    public async Task<EmulatorDto?> GetAsync(string emulatorId, CancellationToken cancellationToken)
    {
        var emulator = await db.Emulators
            .AsNoTracking()
            .Where(e => e.Id == emulatorId)
            .Select(e => new
            {
                Entity = e,
                TagsCount = e.Tags.Count,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return emulator?.Entity.ToDto(emulator.TagsCount);
    }

    /// <summary>
    /// Создает новый эмулятор и публикует runtime-обновление.
    /// </summary>
    /// <param name="request">Параметры создаваемого эмулятора.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Созданный эмулятор.</returns>
    public async Task<EmulatorDto> CreateAsync(CreateEmulatorRequest request, CancellationToken cancellationToken)
    {
        var entity = new EmulatorEntity
        {
            Id = $"em-{Guid.NewGuid():N}"[..12],
            Name = request.Name.Trim(),
            Status = nameof(EmulatorStatus.Stopped),
            ProtocolId = request.ProtocolId,
            TargetUrl = request.TargetUrl.Trim(),
            IntervalSec = Math.Max(1, request.IntervalSec),
        };

        db.Emulators.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        dataCache.InvalidateEmulator(entity.Id);

        var dto = entity.ToDto(tagsCount: 0);
        await runtimeUpdateService.PublishEmulatorUpdatedAsync(dto, cancellationToken);
        return dto;
    }

    /// <summary>
    /// Частично обновляет настройки эмулятора и пересобирает расписание при необходимости.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="request">Новые значения изменяемых полей.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Обновленный эмулятор или <see langword="null"/>, если он не найден.</returns>
    public async Task<EmulatorDto?> PatchAsync(string emulatorId, PatchEmulatorRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.Emulators
            .Include(e => e.Tags)
            .FirstOrDefaultAsync(e => e.Id == emulatorId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var shouldReschedule = false;
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            entity.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.TargetUrl))
        {
            entity.TargetUrl = request.TargetUrl.Trim();
        }

        if (request.ProtocolId is not null)
        {
            entity.ProtocolId = request.ProtocolId.Value;
        }

        if (request.IntervalSec is not null)
        {
            entity.IntervalSec = Math.Max(1, request.IntervalSec.Value);
            if (entity.Status == nameof(EmulatorStatus.Running))
            {
                entity.NextRun = DateTimeOffset.UtcNow.AddSeconds(entity.IntervalSec);
                shouldReschedule = true;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        dataCache.InvalidateEmulator(entity.Id);
        if (shouldReschedule)
        {
            await scheduleService.ScheduleEmulatorAsync(entity.Id, cancellationToken);
        }

        var dto = entity.ToDto(entity.Tags.Count);
        await runtimeUpdateService.PublishEmulatorUpdatedAsync(dto, cancellationToken);
        return dto;
    }

    /// <summary>
    /// Изменяет статус эмулятора, управляет расписанием и публикует событие перехода.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="request">Новый статус эмулятора.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Обновленный эмулятор или <see langword="null"/>, если он не найден.</returns>
    public async Task<EmulatorDto?> PatchStatusAsync(string emulatorId, PatchEmulatorStatusRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.Emulators
            .Include(e => e.Tags)
            .FirstOrDefaultAsync(e => e.Id == emulatorId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var previousStatus = entity.Status;
        entity.Status = request.Status.ToString();
        var wasRunning = previousStatus == nameof(EmulatorStatus.Running);
        var isRunning = request.Status == EmulatorStatus.Running;
        var systemEvent = CreateStatusTransitionEvent(entity, previousStatus, request.Status);

        if (isRunning)
        {
            var now = DateTimeOffset.UtcNow;
            entity.StartedAt ??= now;
            entity.NextRun = now;
            entity.LastError = null;
        }
        else if (request.Status == EmulatorStatus.Stopped)
        {
            entity.StartedAt = null;
            entity.NextRun = null;
        }

        if (systemEvent is not null)
        {
            db.SystemEvents.Add(systemEvent);
        }

        await db.SaveChangesAsync(cancellationToken);
        dataCache.InvalidateEmulator(entity.Id);

        if (isRunning)
        {
            if (!wasRunning)
            {
                await scheduleService.ExecuteEventTagsAsync(entity.Id, TagTriggerEvent.OnStart, cancellationToken);
            }

            await scheduleService.ScheduleEmulatorAsync(entity.Id, cancellationToken);
        }
        else
        {
            if (wasRunning && request.Status == EmulatorStatus.Stopped)
            {
                await scheduleService.ExecuteEventTagsAsync(entity.Id, TagTriggerEvent.OnStop, cancellationToken);
            }

            await scheduleService.UnscheduleEmulatorAsync(entity.Id, cancellationToken);
        }

        var dto = entity.ToDto(entity.Tags.Count);
        await runtimeUpdateService.PublishEmulatorUpdatedAsync(dto, cancellationToken);
        if (systemEvent is not null)
        {
            await runtimeUpdateService.PublishEventCreatedAsync(systemEvent.ToDto(), cancellationToken);
        }

        return dto;
    }

    /// <summary>
    /// Удаляет эмулятор и связанные runtime-расписания.
    /// </summary>
    /// <param name="emulatorId">Идентификатор эмулятора.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns><see langword="true"/>, если эмулятор был удален.</returns>
    public async Task<bool> DeleteAsync(string emulatorId, CancellationToken cancellationToken)
    {
        var deleted = await db.Emulators
            .Where(e => e.Id == emulatorId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            return false;
        }

        await scheduleService.UnscheduleEmulatorAsync(emulatorId, cancellationToken);
        dataCache.InvalidateEmulator(emulatorId);
        dataCache.InvalidateScripts();
        dataCache.InvalidateCncPrograms();
        return true;
    }

    private static SystemEventEntity? CreateStatusTransitionEvent(
        EmulatorEntity emulator,
        string previousStatus,
        EmulatorStatus nextStatus)
    {
        if (previousStatus == nextStatus.ToString())
        {
            return null;
        }

        var (level, message) = nextStatus switch
        {
            EmulatorStatus.Running => (EventLevel.Success, "Эмулятор запущен"),
            EmulatorStatus.Stopped when previousStatus == nameof(EmulatorStatus.Running) => (EventLevel.Info, "Эмулятор остановлен"),
            _ => default,
        };

        if (message is null)
        {
            return null;
        }

        return new SystemEventEntity
        {
            Id = $"ev-{Guid.NewGuid():N}"[..12],
            EmulatorId = emulator.Id,
            EmulatorName = emulator.Name,
            Level = UniEmuJson.EnumString(level),
            Message = message,
            Timestamp = DateTimeOffset.UtcNow,
        };
    }
}
