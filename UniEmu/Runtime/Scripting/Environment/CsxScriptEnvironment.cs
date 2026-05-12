using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Scripting;
using UniEmu.Scripting.Api;

namespace UniEmu.Runtime.Scripting.Environment;

public sealed class CsxScriptEnvironment
{
    private static readonly string[] s_imports =
    [
        "System",
        "System.Collections.Generic",
        "System.Globalization",
        "System.Linq",
        "UniEmu.Scripting.Api",
    ];

    private readonly ConcurrentDictionary<Type, IReadOnlyList<MetadataReference>> metadataReferenceCache = new();

    public CSharpParseOptions ParseOptions { get; } = CSharpParseOptions.Default
        .WithKind(SourceCodeKind.Script)
        .WithLanguageVersion(LanguageVersion.Preview)
        .WithDocumentationMode(DocumentationMode.Diagnose);

    public CSharpCompilationOptions CompilationOptions { get; } = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        .WithUsings(s_imports)
        .WithAllowUnsafe(false)
        .WithOptimizationLevel(OptimizationLevel.Debug);

    public ScriptOptions CreateScriptOptions(
        string entryPath,
        IReadOnlyDictionary<string, string> visibleScripts)
    {
        return ScriptOptions.Default
            .WithReferences(
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(DateTimeOffset).Assembly,
                typeof(TagScriptGlobals).Assembly)
            .WithImports(s_imports)
            .WithFilePath(TagScriptPath.Normalize(entryPath))
            .WithSourceResolver(new DbScriptSourceResolver(visibleScripts));
    }

    public IReadOnlyList<MetadataReference> CreateMetadataReferences(Type globalsType)
    {
        return metadataReferenceCache.GetOrAdd(globalsType, static type =>
        {
            var references = CreateTrustedPlatformReferences();
            AddAssemblyReference(references, typeof(object).Assembly);
            AddAssemblyReference(references, typeof(Enumerable).Assembly);
            AddAssemblyReference(references, typeof(DateTimeOffset).Assembly);
            AddAssemblyReference(references, typeof(TagScriptGlobals).Assembly);
            AddAssemblyReference(references, type.Assembly);

            return references
                .DistinctBy(reference => reference.Display)
                .ToList();
        });
    }

    internal int MetadataReferenceCacheCount => metadataReferenceCache.Count;

    internal void ClearMetadataReferenceCacheForTests()
    {
        metadataReferenceCache.Clear();
    }

    private static void AddAssemblyReference(List<MetadataReference> references, System.Reflection.Assembly assembly)
    {
        if (!string.IsNullOrWhiteSpace(assembly.Location))
        {
            references.Add(CreateMetadataReference(assembly.Location));
        }
    }

    private static List<MetadataReference> CreateTrustedPlatformReferences()
    {
        var value = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsAllowedTrustedPlatformAssembly)
            .Select(CreateMetadataReference)
            .Cast<MetadataReference>()
            .ToList();
    }

    private static bool IsAllowedTrustedPlatformAssembly(string path)
    {
        var fileName = Path.GetFileName(path);
        return !fileName.Equals("UniEmu.dll", StringComparison.OrdinalIgnoreCase);
    }

    private static PortableExecutableReference CreateMetadataReference(string assemblyPath)
    {
        var documentationPath = Path.ChangeExtension(assemblyPath, ".xml");
        var documentation = File.Exists(documentationPath)
            ? XmlDocumentationProvider.CreateFromFile(documentationPath)
            : DocumentationProvider.Default;

        return MetadataReference.CreateFromFile(assemblyPath, documentation: documentation);
    }
}
