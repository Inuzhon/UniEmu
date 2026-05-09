using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Text;
using UniEmu.Common;

namespace UniEmu.Runtime.Scripting;

public sealed class CsxLanguageService
{
    private static readonly Regex LoadDirective = new(
        @"^\s*#\s*load\s+""(?<path>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly ScriptOptions BaseOptions = ScriptOptions.Default
        .WithReferences(
            typeof(object).Assembly,
            typeof(Enumerable).Assembly,
            typeof(DateTimeOffset).Assembly,
            typeof(UniEmuJson).Assembly)
        .WithImports(
            "System",
            "System.Collections.Generic",
            "System.Globalization",
            "System.Linq",
            "UniEmu.Runtime");

    public CsxAnalysisResult Analyze(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null)
    {
        var options = BaseOptions
            .WithFilePath(TagScriptPath.Normalize(entryPath))
            .WithSourceResolver(new DbScriptSourceResolver(visibleScripts));
        var script = CSharpScript.Create<object?>(
            content,
            options,
            globalsType ?? typeof(object));

        var diagnostics = script.Compile()
            .Select(ToCsxDiagnostic)
            .ToList();

        return new CsxAnalysisResult(entryPath, diagnostics);
    }

    public IReadOnlyList<CsxCompletionItem> GetCompletions(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null)
    {
        var expanded = ExpandLoadedScripts(entryPath, content, position, visibleScripts);
        using var workspace = new AdhocWorkspace(MefHostServices.DefaultHost);
        var projectId = ProjectId.CreateNewId("UniEmu.Csx");
        var documentId = DocumentId.CreateNewId(projectId, entryPath);
        var parseOptions = CSharpParseOptions.Default
            .WithKind(SourceCodeKind.Script)
            .WithLanguageVersion(LanguageVersion.Preview);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithUsings(BaseOptions.Imports);

        var references = BaseOptions.MetadataReferences
            .Concat((globalsType ?? typeof(object)).Assembly.GetReferencedAssemblies()
                .Select(assemblyName => MetadataReference.CreateFromFile(AssemblyPath(assemblyName)))
                .Where(reference => reference is not null)!)
            .DistinctBy(reference => reference.Display)
            .ToList();

        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "UniEmu.Csx",
                "UniEmu.Csx",
                LanguageNames.CSharp,
                parseOptions: parseOptions,
                compilationOptions: compilationOptions,
                metadataReferences: references))
            .AddDocument(documentId, Path.GetFileName(entryPath), SourceText.From(expanded.Content));

        var document = solution.GetDocument(documentId);
        if (document is null)
        {
            return [];
        }

        var service = CompletionService.GetService(document);
        if (service is null)
        {
            return [];
        }

        var completionList = service
            .GetCompletionsAsync(document, expanded.Position)
            .GetAwaiter()
            .GetResult();

        return completionList?.ItemsList
            .Select(item => new CsxCompletionItem(
                item.DisplayText,
                item.SortText,
                item.FilterText,
                item.Properties.TryGetValue("SymbolName", out var symbolName) ? symbolName : item.DisplayText))
            .DistinctBy(item => item.Label, StringComparer.Ordinal)
            .OrderBy(item => item.SortText, StringComparer.Ordinal)
            .ThenBy(item => item.Label, StringComparer.Ordinal)
            .ToList()
            ?? [];
    }

    private static ExpandedScript ExpandLoadedScripts(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts)
    {
        var prefix = new List<string>();
        foreach (Match match in LoadDirective.Matches(content))
        {
            var loadPath = ResolveLoadPath(match.Groups["path"].Value, entryPath, visibleScripts);
            if (loadPath is null || !visibleScripts.TryGetValue(loadPath, out var loadedContent))
            {
                continue;
            }

            prefix.Add($"#line 1 \"{loadPath}\"");
            prefix.Add(loadedContent);
            prefix.Add("#line default");
        }

        if (prefix.Count == 0)
        {
            return new ExpandedScript(content, Math.Clamp(position, 0, content.Length));
        }

        var prefixText = string.Join(Environment.NewLine, prefix) + Environment.NewLine;
        return new ExpandedScript(prefixText + content, Math.Clamp(position, 0, content.Length) + prefixText.Length);
    }

    private static string? ResolveLoadPath(
        string path,
        string baseFilePath,
        IReadOnlyDictionary<string, string> scripts)
    {
        var normalized = TagScriptPath.Normalize(path);
        if (scripts.ContainsKey(normalized))
        {
            return normalized;
        }

        var baseDir = Path.GetDirectoryName(baseFilePath.Replace('\\', '/'))?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            return null;
        }

        var relative = TagScriptPath.Normalize($"{baseDir}/{path}");
        return scripts.ContainsKey(relative) ? relative : null;
    }

    private static CsxDiagnostic ToCsxDiagnostic(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan();
        var start = span.StartLinePosition;
        var end = span.EndLinePosition;
        return new CsxDiagnostic(
            diagnostic.Id,
            diagnostic.GetMessage(),
            diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => CsxDiagnosticSeverity.Error,
                DiagnosticSeverity.Warning => CsxDiagnosticSeverity.Warning,
                DiagnosticSeverity.Info => CsxDiagnosticSeverity.Information,
                _ => CsxDiagnosticSeverity.Hint,
            },
            start.Line,
            start.Character,
            end.Line,
            end.Character);
    }

    private static string AssemblyPath(System.Reflection.AssemblyName assemblyName)
    {
        try
        {
            return System.Reflection.Assembly.Load(assemblyName).Location;
        }
        catch
        {
            return typeof(object).Assembly.Location;
        }
    }

    private sealed record ExpandedScript(string Content, int Position);
}

public sealed record CsxAnalysisResult(
    string EntryPath,
    IReadOnlyList<CsxDiagnostic> Diagnostics);

public sealed record CsxDiagnostic(
    string Code,
    string Message,
    CsxDiagnosticSeverity Severity,
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter);

public enum CsxDiagnosticSeverity
{
    Error = 1,
    Warning = 2,
    Information = 3,
    Hint = 4,
}

public sealed record CsxCompletionItem(
    string Label,
    string SortText,
    string FilterText,
    string InsertText);
