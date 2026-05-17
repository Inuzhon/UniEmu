using Microsoft.EntityFrameworkCore;
using UniEmu.Domain.Entities;

namespace UniEmu.Data;

/// <summary>
/// EF Core-контекст основной базы данных UniEmu.
/// </summary>
public sealed class UniEmuDbContext(DbContextOptions<UniEmuDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Эмуляторы и их runtime-настройки.
    /// </summary>
    public DbSet<EmulatorEntity> Emulators => Set<EmulatorEntity>();

    /// <summary>
    /// Теги эмуляторов и их конфигурации расчета.
    /// </summary>
    public DbSet<EmulatorTagEntity> EmulatorTags => Set<EmulatorTagEntity>();

    /// <summary>
    /// CSX-скрипты, доступные runtime и редактору.
    /// </summary>
    public DbSet<ScriptFileEntity> ScriptFiles => Set<ScriptFileEntity>();

    /// <summary>
    /// Persistent state CSX-скриптов.
    /// </summary>
    public DbSet<ScriptRuntimeStateEntity> ScriptRuntimeStates => Set<ScriptRuntimeStateEntity>();

    /// <summary>
    /// CNC-программы для передачи через Dispatcher.
    /// </summary>
    public DbSet<CncProgramEntity> CncPrograms => Set<CncProgramEntity>();

    /// <summary>
    /// История точек телеметрии.
    /// </summary>
    public DbSet<TelemetryPointEntity> TelemetryPoints => Set<TelemetryPointEntity>();

    /// <summary>
    /// Системные события эмуляторов.
    /// </summary>
    public DbSet<SystemEventEntity> SystemEvents => Set<SystemEventEntity>();

    /// <summary>
    /// Настраивает ключи, индексы, ограничения строк и каскадное удаление доменных сущностей.
    /// </summary>
    /// <param name="modelBuilder">Построитель модели EF Core.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmulatorEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
            entity.Property(e => e.ProtocolId).IsRequired();
            entity.Property(e => e.TargetUrl).HasMaxLength(2048).IsRequired();
            entity.HasMany(e => e.Tags)
                .WithOne(t => t.Emulator)
                .HasForeignKey(t => t.EmulatorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmulatorTagEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.EmulatorId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Key).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Source).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Enabled).IsRequired().HasDefaultValue(true);
            entity.HasIndex(e => e.EmulatorId);
        });

        modelBuilder.Entity<ScriptFileEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Name).HasMaxLength(260).IsRequired();
            entity.Property(e => e.Scope).HasMaxLength(32).IsRequired();
            entity.Property(e => e.EmulatorId).HasMaxLength(64);
            entity.HasOne<EmulatorEntity>()
                .WithMany()
                .HasForeignKey(e => e.EmulatorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.Scope, e.EmulatorId, e.Name }).IsUnique();
        });

        modelBuilder.Entity<ScriptRuntimeStateEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.EmulatorId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ScriptKey).HasMaxLength(260).IsRequired();
            entity.Property(e => e.ValuesJson).IsRequired();
            entity.HasOne<EmulatorEntity>()
                .WithMany()
                .HasForeignKey(e => e.EmulatorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.EmulatorId, e.ScriptKey }).IsUnique();
        });

        modelBuilder.Entity<CncProgramEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Name).HasMaxLength(260).IsRequired();
            entity.Property(e => e.Scope).HasMaxLength(32).IsRequired();
            entity.Property(e => e.EmulatorId).HasMaxLength(64);
            entity.HasOne<EmulatorEntity>()
                .WithMany()
                .HasForeignKey(e => e.EmulatorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.Scope, e.EmulatorId, e.Name }).IsUnique();
        });

        modelBuilder.Entity<TelemetryPointEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmulatorId).HasMaxLength(64).IsRequired();
            entity.HasOne<EmulatorEntity>()
                .WithMany()
                .HasForeignKey(e => e.EmulatorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.EmulatorId, e.Timestamp });
        });

        modelBuilder.Entity<SystemEventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.EmulatorId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Level).HasMaxLength(32).IsRequired();
            entity.HasOne<EmulatorEntity>()
                .WithMany()
                .HasForeignKey(e => e.EmulatorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.Timestamp);
        });
    }
}
