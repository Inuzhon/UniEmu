using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniEmu.Contracts.Enums;
using UniEmu.Data;
using UniEmu.Domain.Entities;
using UniEmu.Runtime.Scripting;

namespace UniEmu.Tests.Runtime.Scripting;

public sealed class CsxIntellisenseServiceTests
{
    private const string MachineDocumentUri = "uniemu://scripts/scr-machine/machine.csx?name=machine.csx&scope=emulator&emulatorId=em-1";

    [Fact]
    public async Task GetDiagnosticsAsync_UsesUnsavedDocumentAndVisibleLoadedScripts()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());

        var diagnostics = await service.GetDiagnosticsAsync(new CsxIntellisenseRequest(
            "#load \"common.csx\"\nreturn Add(1, 2);",
            MachineDocumentUri,
            null), CancellationToken.None);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == CsxDiagnosticSeverity.Error);
    }

    [Fact]
    public async Task GetDiagnosticsAsync_DoesNotProjectLoadedScriptDiagnosticsOntoEntryDocument()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var sharedScript = await db.ScriptFiles.SingleAsync(script => script.Name == "common.csx");
        sharedScript.Content = "#r \"System.Text.Json.dll\"\nint Add(int a, int b) => a + b;";
        await db.SaveChangesAsync();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());

        var diagnostics = await service.GetDiagnosticsAsync(new CsxIntellisenseRequest(
            "#load \"common.csx\"\nreturn Add(1, 2);",
            MachineDocumentUri,
            null), CancellationToken.None);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Code == "CSX001");
    }

    [Fact]
    public async Task GetDiagnosticsAsync_DoesNotProjectLoadedCompilerDiagnosticsOntoEntryDocument()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var sharedScript = await db.ScriptFiles.SingleAsync(script => script.Name == "common.csx");
        sharedScript.Content = "int Add(int a, int b) => MissingLoaded;";
        await db.SaveChangesAsync();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());

        var diagnostics = await service.GetDiagnosticsAsync(new CsxIntellisenseRequest(
            "#load \"common.csx\"\nreturn Add(1, 2);",
            MachineDocumentUri,
            null), CancellationToken.None);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Code == "CS0103");
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
            MachineDocumentUri,
            new CsxEditorPosition(2, 10)), CancellationToken.None);

        Assert.Contains(completions, item => item.Label == "LoadedHelper");
    }

    [Fact]
    public async Task GetCompletionsAsync_ParsesEncodedInlineDocumentUriAndReturnsUniEmuMembers()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());

        var completions = await service.GetCompletionsAsync(new CsxIntellisenseRequest(
            "UniEmu.",
            "uniemu://scripts/tg-f75b656d2/inline/tg-f75b656d2.csx?name%3Dinline%2Ftg-f75b656d2.csx%26scope%3Demulator%26emulatorId%3Dem-1",
            new CsxEditorPosition(1, 8)), CancellationToken.None);

        Assert.Contains(completions, item => item.Label == "Tag");
        Assert.Contains(completions, item => item.Label == "Tags");
        Assert.Contains(completions, item => item.Label == "State");
        Assert.Contains(completions, item => item.Label == "Emulator");
    }

    [Fact]
    public async Task GetHoverAsync_ReturnsScriptingApiSymbolHover()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());
        const string source = "return UniEmu.Tags;";

        var hover = await service.GetHoverAsync(new CsxIntellisenseRequest(
            source,
            MachineDocumentUri,
            PositionAt(source, "Tags")), CancellationToken.None);

        Assert.NotNull(hover);
        Assert.Contains("Tags", hover.Signature, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSignatureHelpAsync_ReturnsMethodOverloads()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());
        const string source = "var value = Math.Round(";

        var signatureHelp = await service.GetSignatureHelpAsync(new CsxIntellisenseRequest(
            source,
            MachineDocumentUri,
            PositionAt(source, "(", characterOffset: 1)), CancellationToken.None);

        Assert.NotNull(signatureHelp);
        Assert.Contains(signatureHelp.Signatures, signature =>
            signature.Label.Contains("Round", StringComparison.Ordinal)
            && signature.Parameters.Count > 0);
    }

    [Fact]
    public async Task GetDefinitionsAsync_MapsEntryDocumentPathToRequestUri()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());
        const string source = """
            int Add(int left, int right) => left + right;
            return Add(1, 2);
            """;

        var definitions = await service.GetDefinitionsAsync(new CsxIntellisenseRequest(
            source,
            MachineDocumentUri,
            PositionAt(source, "Add", occurrence: 2)), CancellationToken.None);

        var definition = Assert.Single(definitions);
        Assert.Equal(MachineDocumentUri, definition.DocumentPath);
        Assert.Equal(0, definition.Range.StartLine);
        Assert.Equal(4, definition.Range.StartCharacter);
    }

    [Fact]
    public async Task GetTypeDefinitionsAsync_ReturnsLocalTypeDeclaration()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());
        const string source = """
            class SensorReading
            {
                public int Value { get; set; }
            }

            var reading = new SensorReading();
            return reading.Value;
            """;

        var definitions = await service.GetTypeDefinitionsAsync(new CsxIntellisenseRequest(
            source,
            MachineDocumentUri,
            PositionAt(source, "reading.Value")), CancellationToken.None);

        var definition = Assert.Single(definitions);
        Assert.Equal(MachineDocumentUri, definition.DocumentPath);
        Assert.Equal(0, definition.Range.StartLine);
        Assert.Equal(6, definition.Range.StartCharacter);
    }

    [Fact]
    public async Task GetReferencesAsync_UsesVisibleLoadedScripts()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());
        const string source = "#load \"common.csx\"\nreturn LoadedHelper(2);";

        var references = await service.GetReferencesAsync(new CsxIntellisenseRequest(
            source,
            MachineDocumentUri,
            new CsxEditorPosition(2, 13)), CancellationToken.None);

        Assert.Contains(references, reference => reference.DocumentPath == "common.csx");
        Assert.Contains(references, reference => reference.DocumentPath.StartsWith("uniemu://scripts/scr-machine", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetImplementationsAsync_ReturnsLocalImplementation()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());
        const string source = """
            interface ISample
            {
                int Read();
            }

            class Sample : ISample
            {
                public int Read() => 1;
            }

            return new Sample().Read();
            """;

        var implementations = await service.GetImplementationsAsync(new CsxIntellisenseRequest(
            source,
            MachineDocumentUri,
            PositionAt(source, "ISample")), CancellationToken.None);

        Assert.Contains(implementations, implementation =>
            implementation.DocumentPath == MachineDocumentUri
            && implementation.Range.StartLine == 5
            && implementation.Range.StartCharacter == 6);
    }

    [Fact]
    public async Task RenameAsync_ReturnsOnlyRequestDocumentEdits()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());
        const string source = "int LocalValue() => 1;\nreturn LocalValue();";

        var edit = await service.RenameAsync(new CsxIntellisenseRequest(
            source,
            MachineDocumentUri,
            new CsxEditorPosition(2, 10),
            NewName: "RenamedValue"), CancellationToken.None);

        Assert.NotNull(edit);
        var documentEdit = Assert.Single(edit.DocumentEdits);
        Assert.Equal(MachineDocumentUri, documentEdit.DocumentPath);
        Assert.Equal(2, documentEdit.Edits.Count);
    }

    [Fact]
    public async Task RenameAsync_ReturnsNull_WhenNewNameIsBlank()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());

        var edit = await service.RenameAsync(new CsxIntellisenseRequest(
            "int LocalValue() => 1;",
            MachineDocumentUri,
            new CsxEditorPosition(1, 5),
            NewName: " "), CancellationToken.None);

        Assert.Null(edit);
    }

    [Fact]
    public async Task FormatDocumentAsync_ReturnsWholeDocumentEdit()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());

        var edits = await service.FormatDocumentAsync(new CsxIntellisenseRequest(
            "if(true){return 1;}",
            MachineDocumentUri,
            null), CancellationToken.None);

        var edit = Assert.Single(edits);
        Assert.Equal("if (true) { return 1; }", edit.NewText);
    }

    [Fact]
    public async Task FormatRangeAsync_UsesEmptyRangeWhenRequestRangeIsMissing()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());

        var edits = await service.FormatRangeAsync(new CsxIntellisenseRequest(
            "if(true){return 1;}",
            MachineDocumentUri,
            null), CancellationToken.None);

        var edit = Assert.Single(edits);
        Assert.Equal("if (true) { return 1; }", edit.NewText);
    }

    [Fact]
    public async Task GetFoldingRangesAsync_ReturnsBlockRanges()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());
        const string source = """
            if (true)
            {
                return 1;
            }
            return 0;
            """;

        var ranges = await service.GetFoldingRangesAsync(new CsxIntellisenseRequest(
            source,
            MachineDocumentUri,
            null), CancellationToken.None);

        Assert.Contains(ranges, range => range.StartLine == 1 && range.EndLine == 3);
    }

    [Fact]
    public async Task GetSemanticTokensAsync_ReturnsTokenData()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());

        var tokens = await service.GetSemanticTokensAsync(new CsxIntellisenseRequest(
            "var pressure = UniEmu.Tag.Name;",
            MachineDocumentUri,
            null), CancellationToken.None);

        Assert.NotEmpty(tokens.Data);
        Assert.Contains("variable", tokens.Legend.TokenTypes);
        Assert.Contains("property", tokens.Legend.TokenTypes);
    }

    [Fact]
    public async Task PrepareCallHierarchyAsync_MapsEntryDocumentPathToRequestUri()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());
        const string source = """
            int Add(int left, int right) => left + right;
            int Twice() => Add(1, 2);
            return Twice();
            """;

        var items = await service.PrepareCallHierarchyAsync(new CsxIntellisenseRequest(
            source,
            MachineDocumentUri,
            PositionAt(source, "Twice")), CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal("Twice", item.Name);
        Assert.Equal(MachineDocumentUri, item.DocumentPath);
    }

    [Fact]
    public async Task GetIncomingCallsAsync_MapsCallerDocumentPathToRequestUri()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());
        const string source = """
            int Add(int left, int right) => left + right;
            int Twice() => Add(1, 2);
            return Twice();
            """;

        var calls = await service.GetIncomingCallsAsync(new CsxIntellisenseRequest(
            source,
            MachineDocumentUri,
            PositionAt(source, "Add")), CancellationToken.None);

        var call = Assert.Single(calls);
        Assert.Equal("Twice", call.From.Name);
        Assert.Equal(MachineDocumentUri, call.From.DocumentPath);
        Assert.NotEmpty(call.FromRanges);
    }

    [Fact]
    public async Task GetOutgoingCallsAsync_MapsCalleeDocumentPathToRequestUri()
    {
        await using var fixture = await IntellisenseDbFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        var service = new CsxIntellisenseService(db, new CsxLanguageService());
        const string source = """
            int Add(int left, int right) => left + right;
            int Twice() => Add(1, 2);
            return Twice();
            """;

        var calls = await service.GetOutgoingCallsAsync(new CsxIntellisenseRequest(
            source,
            MachineDocumentUri,
            PositionAt(source, "Twice")), CancellationToken.None);

        var call = Assert.Single(calls);
        Assert.Equal("Add", call.To.Name);
        Assert.Equal(MachineDocumentUri, call.To.DocumentPath);
    }

    private static CsxEditorPosition PositionAt(string source, string value, int occurrence = 1, int characterOffset = 0)
    {
        var offset = -1;
        for (var i = 0; i < occurrence; i++)
        {
            offset = source.IndexOf(value, offset + 1, StringComparison.Ordinal);
            if (offset < 0)
            {
                throw new InvalidOperationException($"Could not find occurrence {occurrence} of '{value}'.");
            }
        }

        offset += characterOffset;
        var line = 1;
        var lineStart = 0;
        for (var i = 0; i < offset; i++)
        {
            if (source[i] != '\n')
            {
                continue;
            }

            line++;
            lineStart = i + 1;
        }

        return new CsxEditorPosition(line, offset - lineStart + 1);
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
