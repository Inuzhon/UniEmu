using Microsoft.EntityFrameworkCore;
using UniEmu.Domain.Entities;

namespace UniEmu.Data;

public sealed class UniEmuDbContext(DbContextOptions<UniEmuDbContext> options) : DbContext(options)
{
    public DbSet<EmulatorEntity> Emulators => Set<EmulatorEntity>();

    public DbSet<EmulatorTagEntity> EmulatorTags => Set<EmulatorTagEntity>();

    public DbSet<ScriptFileEntity> ScriptFiles => Set<ScriptFileEntity>();

    public DbSet<CncProgramEntity> CncPrograms => Set<CncProgramEntity>();

    public DbSet<TelemetryPointEntity> TelemetryPoints => Set<TelemetryPointEntity>();

    public DbSet<SystemEventEntity> SystemEvents => Set<SystemEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmulatorEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
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
            entity.HasIndex(e => e.EmulatorId);
        });

        modelBuilder.Entity<ScriptFileEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Name).HasMaxLength(260).IsRequired();
            entity.Property(e => e.Scope).HasMaxLength(32).IsRequired();
            entity.Property(e => e.EmulatorId).HasMaxLength(64);
            entity.HasIndex(e => new { e.Scope, e.EmulatorId, e.Name }).IsUnique();
        });

        modelBuilder.Entity<CncProgramEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Name).HasMaxLength(260).IsRequired();
            entity.Property(e => e.Scope).HasMaxLength(32).IsRequired();
            entity.Property(e => e.EmulatorId).HasMaxLength(64);
            entity.HasIndex(e => new { e.Scope, e.EmulatorId, e.Name }).IsUnique();
        });

        modelBuilder.Entity<TelemetryPointEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmulatorId).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => new { e.EmulatorId, e.Timestamp });
        });

        modelBuilder.Entity<SystemEventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.EmulatorId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Level).HasMaxLength(32).IsRequired();
            entity.HasIndex(e => e.Timestamp);
        });
    }
}
