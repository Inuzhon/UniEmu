using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Text;
using UniEmu.Common;

namespace UniEmu.Runtime.Scripting;

public sealed class CsxLanguageService
{
    private static readonly Regex s_loadDirective = new(
        @"^\s*#\s*load\s+""(?<path>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly ScriptOptions s_baseOptions = ScriptOptions.Default
        .WithReferences(
            typeof(object).Assembly,
            typeof(Enumerable).Assembly,
            typeof(DateTimeOffset).Assembly
        //typeof(UniEmuJson).Assembly
        )
        .WithImports(
            "System",
            "System.Collections.Generic",
            "System.Globalization",
            "System.Linq",
            "UniEmu.Runtime.Scripting.UserScripts");
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<MetadataReference>> s_metadataReferenceCache = new();

    internal static int MetadataReferenceCacheCount => s_metadataReferenceCache.Count;

    internal static void ClearMetadataReferenceCacheForTests()
    {
        s_metadataReferenceCache.Clear();
    }

    public CsxAnalysisResult Analyze(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null)
    {
        var options = s_baseOptions
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
        var document = CreateDocument(workspace, entryPath, expanded.Content, globalsType);

        var service = CompletionService.GetService(document);
        if (service is null)
            return [];

        var completionList = service
            .GetCompletionsAsync(document, expanded.Position)
            .GetAwaiter()
            .GetResult();

        return completionList?.ItemsList
            .Select(item => new CsxCompletionItem(
                item.DisplayText,
                item.SortText,
                item.FilterText,
                item.Properties.TryGetValue("SymbolName", out var symbolName) ? symbolName : item.DisplayText,
                item.InlineDescription,
                null,
                GetCompletionKind(item.Tags)))
            .DistinctBy(item => item.Label, StringComparer.Ordinal)
            .OrderBy(item => item.SortText, StringComparer.Ordinal)
            .ThenBy(item => item.Label, StringComparer.Ordinal)
            .ToList() ?? [];
    }

    public CsxHover? GetHover(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null)
    {
        var expanded = ExpandLoadedScripts(entryPath, content, position, visibleScripts);
        using var workspace = new AdhocWorkspace(MefHostServices.DefaultHost);
        var document = CreateDocument(workspace, entryPath, expanded.Content, globalsType);
        var root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
        var semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult();

        if (root is null || semanticModel is null || expanded.Content.Length == 0)
            return null;

        var token = root.FindToken(Math.Clamp(expanded.Position, 0, Math.Max(0, expanded.Content.Length - 1)));
        var symbol = ResolveSymbol(semanticModel, token);
        if (symbol is null)
        {
            return null;
        }

        return new CsxHover(
            symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            FormatDocumentation(symbol),
            token.SpanStart,
            token.Span.End);
    }

    public CsxSignatureHelp? GetSignatureHelp(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null)
    {
        var expanded = ExpandLoadedScripts(entryPath, content, position, visibleScripts);
        using var workspace = new AdhocWorkspace(MefHostServices.DefaultHost);
        var document = CreateDocument(workspace, entryPath, expanded.Content, globalsType);
        var root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
        var semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult();

        if (root is null || semanticModel is null)
            return null;

        var tokenPosition = Math.Clamp(expanded.Position - 1, 0, Math.Max(0, expanded.Content.Length - 1));
        var argumentList = root
            .FindToken(tokenPosition)
            .Parent?
            .AncestorsAndSelf()
            .OfType<BaseArgumentListSyntax>()
            .FirstOrDefault(argumentList => argumentList.Span.Start <= expanded.Position && expanded.Position <= argumentList.Span.End);

        if (argumentList is null)
            return null;

        var methods = ResolveCallableSymbols(semanticModel, argumentList)
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<IMethodSymbol>()
            .OrderBy(method => method.Parameters.Length)
            .ThenBy(method => method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), StringComparer.Ordinal)
            .ToArray();

        if (methods.Length == 0)
            return null;

        return new CsxSignatureHelp(
            methods.Select(method => new CsxSignature(
                    method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    FormatDocumentation(method),
                    method.Parameters
                        .Select(parameter => new CsxSignatureParameter(
                            parameter.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            FormatDocumentation(parameter)))
                        .ToArray()))
                .ToArray(),
            0,
            GetActiveParameter(argumentList, expanded.Position));
    }

    private static Document CreateDocument(
        AdhocWorkspace workspace,
        string entryPath,
        string content,
        Type? globalsType)
    {
        var projectId = ProjectId.CreateNewId("UniEmu.Csx");
        var documentId = DocumentId.CreateNewId(projectId, entryPath);
        var parseOptions = CSharpParseOptions.Default
            .WithKind(SourceCodeKind.Script)
            .WithLanguageVersion(LanguageVersion.Preview);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithUsings(s_baseOptions.Imports);

        var references = CreateMetadataReferences(globalsType ?? typeof(object));
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
            .AddDocument(documentId, Path.GetFileName(entryPath), SourceText.From(content));

        return solution.GetDocument(documentId)
               ?? throw new InvalidOperationException("Unable to create CSX Roslyn document.");
    }

    private static IReadOnlyList<MetadataReference> CreateMetadataReferences(Type globalsType)
    {
        return s_metadataReferenceCache.GetOrAdd(globalsType, static type => s_baseOptions.MetadataReferences
            //TODO: Не надо запихивать в подсказки весь проект
            //.Concat([MetadataReference.CreateFromFile(type.Assembly.Location)])
            //.Concat(type.Assembly.GetReferencedAssemblies()
            //    .Select(assemblyName => MetadataReference.CreateFromFile(AssemblyPath(assemblyName))))
            .DistinctBy(reference => reference.Display)
            .ToList());
    }

    private static ExpandedScript ExpandLoadedScripts(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts)
    {
        var prefix = new List<string>();
        foreach (Match match in s_loadDirective.Matches(content))
        {
            var loadPath = ResolveLoadPath(match.Groups["path"].Value, entryPath, visibleScripts);
            if (loadPath is null || !visibleScripts.TryGetValue(loadPath, out var loadedContent))
                continue;

            prefix.Add($"#line 1 \"{loadPath}\"");
            prefix.Add(loadedContent);
            prefix.Add("#line default");
        }

        if (prefix.Count == 0)
            return new ExpandedScript(content, Math.Clamp(position, 0, content.Length));

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
            return normalized;

        var baseDir = Path.GetDirectoryName(baseFilePath.Replace('\\', '/'))?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(baseDir))
            return null;

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

    private static ISymbol? ResolveSymbol(SemanticModel semanticModel, SyntaxToken token)
    {
        foreach (var node in token.Parent?.AncestorsAndSelf() ?? [])
        {
            var symbol = semanticModel.GetSymbolInfo(node).Symbol
                         ?? semanticModel.GetDeclaredSymbol(node)
                         ?? semanticModel.GetSymbolInfo(node).CandidateSymbols.FirstOrDefault();
            if (symbol is not null)
                return symbol;
        }

        return null;
    }

    private static IEnumerable<ISymbol> ResolveCallableSymbols(SemanticModel semanticModel, BaseArgumentListSyntax argumentList)
    {
        return argumentList.Parent switch
        {
            InvocationExpressionSyntax invocation => ResolveInvocationSymbols(semanticModel, invocation),
            ObjectCreationExpressionSyntax creation => ResolveCreationSymbols(semanticModel, creation),
            _ => [],
        };
    }

    private static IEnumerable<ISymbol> ResolveInvocationSymbols(SemanticModel semanticModel, InvocationExpressionSyntax invocation)
    {
        var group = semanticModel.GetMemberGroup(invocation.Expression);
        if (group.Length > 0)
            return group;

        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        return symbolInfo.Symbol is not null
            ? [symbolInfo.Symbol]
            : symbolInfo.CandidateSymbols;
    }

    private static IEnumerable<ISymbol> ResolveCreationSymbols(SemanticModel semanticModel, ObjectCreationExpressionSyntax creation)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(creation);
        return symbolInfo.Symbol is not null
            ? [symbolInfo.Symbol]
            : symbolInfo.CandidateSymbols;
    }

    private static int GetActiveParameter(BaseArgumentListSyntax argumentList, int position)
    {
        var activeParameter = 0;

        foreach (var argument in argumentList.Arguments)
        {
            if (argument.Span.End < position)
                activeParameter++;
        }

        return activeParameter;
    }

    private static string GetCompletionKind(IEnumerable<string> tags)
    {
        var tag = tags.FirstOrDefault()?.ToLowerInvariant();
        return tag switch
        {
            "method" => "method",
            "property" => "property",
            "class" => "class",
            "struct" => "struct",
            "enum" => "enum",
            "field" => "field",
            "keyword" => "keyword",
            "local" => "variable",
            "parameter" => "variable",
            _ => "text",
        };
    }

    private static string? FormatDocumentation(ISymbol symbol)
    {
        var documentation = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(documentation))
            return null;

        return Regex.Replace(documentation, "<.*?>", " ")
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
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
    string InsertText,
    string? Detail = null,
    string? Documentation = null,
    string Kind = "text");

public sealed record CsxHover(
    string Signature,
    string? Documentation,
    int StartOffset,
    int EndOffset);

public sealed record CsxSignatureHelp(
    IReadOnlyList<CsxSignature> Signatures,
    int ActiveSignature,
    int ActiveParameter);

public sealed record CsxSignature(
    string Label,
    string? Documentation,
    IReadOnlyList<CsxSignatureParameter> Parameters);

public sealed record CsxSignatureParameter(
    string Label,
    string? Documentation);
