using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniEmu.Data;
using UniEmu.Domain.Entities;

namespace UniEmu.Tests.Data;

public sealed class UniEmuDbContextRelationshipTests
{
    [Fact]
    public async Task DeletingEmulator_CascadesToAllEmulatorOwnedRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<UniEmuDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new UniEmuDbContext(options))
        {
            await db.Database.MigrateAsync();
            var now = DateTimeOffset.UtcNow;

            db.Emulators.Add(new EmulatorEntity
            {
                Id = "em-owned",
                Name = "Owned rows emulator",
                Status = "Stopped",
                ProtocolId = 1,
                TargetUrl = "http://localhost",
                IntervalSec = 1,
            });
            db.EmulatorTags.Add(new EmulatorTagEntity
            {
                Id = "tag-owned",
                EmulatorId = "em-owned",
                Name = "Tag",
                Key = "tag",
                Type = "double",
                Source = "static",
                Preview = "1",
                TriggerJson = "{}",
            });
            db.ScriptFiles.Add(new ScriptFileEntity
            {
                Id = "script-owned",
                EmulatorId = "em-owned",
                Name = "machine.csx",
                Scope = "emulator",
                Content = "return 1;",
                UpdatedAt = now,
                SizeBytes = 9,
            });
            db.CncPrograms.Add(new CncProgramEntity
            {
                Id = "cnc-owned",
                EmulatorId = "em-owned",
                Name = "machine.nc",
                Scope = "emulator",
                Content = "M30",
                Description = "Owned program",
                UpdatedAt = now,
                UploadedAt = now,
                SizeBytes = 3,
            });
            db.TelemetryPoints.Add(new TelemetryPointEntity
            {
                EmulatorId = "em-owned",
                Timestamp = now,
                ValuesJson = "{}",
            });
            db.SystemEvents.Add(new SystemEventEntity
            {
                Id = "event-owned",
                EmulatorId = "em-owned",
                EmulatorName = "Owned rows emulator",
                Level = "info",
                Message = "Created",
                Timestamp = now,
            });
            db.ScriptRuntimeStates.Add(new ScriptRuntimeStateEntity
            {
                Id = "state-owned",
                EmulatorId = "em-owned",
                ScriptKey = "machine.csx",
                ValuesJson = "{}",
                UpdatedAt = now,
            });

            await db.SaveChangesAsync();
        }

        await using (var db = new UniEmuDbContext(options))
        {
            await db.Emulators
                .Where(e => e.Id == "em-owned")
                .ExecuteDeleteAsync();
        }

        await using (var db = new UniEmuDbContext(options))
        {
            Assert.Empty(await db.EmulatorTags.ToListAsync());
            Assert.Empty(await db.ScriptFiles.ToListAsync());
            Assert.Empty(await db.CncPrograms.ToListAsync());
            Assert.Empty(await db.TelemetryPoints.ToListAsync());
            Assert.Empty(await db.SystemEvents.ToListAsync());
            Assert.Empty(await db.ScriptRuntimeStates.ToListAsync());
        }
    }
}
