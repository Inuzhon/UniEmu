using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Runtime.Scripting;

namespace UniEmu.Tests.Runtime.Scripting;

public sealed class CsxIntellisenseServiceTests
{
    [Fact]
    public async Task GetDiagnosticsAsync_UsesUnsavedDocumentAndVisibleLoadedScripts()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());

        var diagnostics = await service.GetDiagnosticsAsync(new CsxIntellisenseRequest(
            "#load \"common.csx\"\nreturn Add(1, 2);",
            "uniemu://scripts/scr-machine/machine.csx?name=machine.csx&scope=emulator&emulatorId=em-1",
            null), CancellationToken.None);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == CsxDiagnosticSeverity.Error);
    }

    [Fact]
    public async Task GetCompletionsAsync_ReturnsLoadedScriptSymbolsFromRestRequest()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());
        const string source = "#load \"common.csx\"\nreturn Lo";

        var completions = await service.GetCompletionsAsync(new CsxIntellisenseRequest(
            source,
            "uniemu://scripts/scr-machine/machine.csx?name=machine.csx&scope=emulator&emulatorId=em-1",
            new CsxEditorPosition(2, 10)), CancellationToken.None);

        Assert.Contains(completions, item => item.Label == "LoadedHelper");
    }

    private sealed class IntellisenseDbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<UniEmuDbContext> options;

        private IntellisenseDbFixture(SqliteConnection connection, DbContextOptions<UniEmuDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<IntellisenseDbFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<UniEmuDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var db = new UniEmuDbContext(options);
            await db.Database.EnsureCreatedAsync();
            await SeedAsync(db);

            return new IntellisenseDbFixture(connection, options);
        }

        public UniEmuDbContext CreateDbContext() => new(options);

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }

        private static async Task SeedAsync(UniEmuDbContext db)
        {
            var now = DateTimeOffset.UtcNow;
            db.Emulators.Add(new EmulatorEntity
            {
                Id = "em-1",
                Name = "Main emulator",
                Status = nameof(EmulatorStatus.Running),
                ProtocolId = 18,
                TargetUrl = "http://localhost",
                IntervalSec = 1,
            });

            db.ScriptFiles.AddRange(
                new ScriptFileEntity
                {
                    Id = "scr-shared",
                    Name = "common.csx",
                    Scope = "shared",
                    Content = "double LoadedHelper(double value) => value * 2;\nint Add(int a, int b) => a + b;",
                    SizeBytes = 80,
                    UpdatedAt = now,
                },
                new ScriptFileEntity
                {
                    Id = "scr-machine",
                    Name = "machine.csx",
                    Scope = "emulator",
                    EmulatorId = "em-1",
                    Content = "return 1;",
                    SizeBytes = 9,
                    UpdatedAt = now,
                });

            await db.SaveChangesAsync();
        }
    }
}
