using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Scripting;
using UniEmu.Scripting.Api;

namespace UniEmu.Runtime.Scripting.Environment;

public sealed class CsxScriptEnvironment
{
    private static readonly HashSet<string> s_allowedTrustedPlatformAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.CSharp.dll",
        "mscorlib.dll",
        "netstandard.dll",
        "System.Collections.Concurrent.dll",
        "System.Collections.dll",
        "System.ComponentModel.dll",
        "System.ComponentModel.Primitives.dll",
        "System.Console.dll",
        "System.Globalization.dll",
        "System.Linq.dll",
        "System.Linq.Expressions.dll",
        "System.ObjectModel.dll",
        "System.Private.CoreLib.dll",
        "System.Private.Uri.dll",
        "System.Runtime.dll",
        "System.Runtime.Extensions.dll",
        "System.Threading.dll",
        "System.Threading.Tasks.dll",
        "UniEmu.Scripting.Api.dll",
    };

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
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null)
    {
        return ScriptOptions.Default
            .WithReferences(CreateMetadataReferences(globalsType ?? typeof(TagScriptGlobals)))
            .WithImports(s_imports)
            .WithFilePath(TagScriptPath.Normalize(entryPath))
            .WithSourceResolver(new DbScriptSourceResolver(visibleScripts));
    }

    public IReadOnlyList<MetadataReference> CreateMetadataReferences(Type globalsType)
    {
        return metadataReferenceCache.GetOrAdd(globalsType, static type =>
        {
            var references = CreateTrustedPlatformReferences(type);

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

    private static List<MetadataReference> CreateTrustedPlatformReferences(Type globalsType)
    {
        var value = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var globalsAssemblyPath = globalsType.Assembly.Location;
        var references = value
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(path => IsAllowedTrustedPlatformAssembly(path, globalsAssemblyPath))
            .Select(CreateMetadataReference)
            .Cast<MetadataReference>()
            .ToList();

        if (!string.IsNullOrWhiteSpace(globalsAssemblyPath)
            && File.Exists(globalsAssemblyPath)
            && references.All(reference => !string.Equals(reference.Display, globalsAssemblyPath, StringComparison.OrdinalIgnoreCase)))
        {
            references.Add(CreateMetadataReference(globalsAssemblyPath));
        }

        return references;
    }

    private static bool IsAllowedTrustedPlatformAssembly(string path, string globalsAssemblyPath)
    {
        var fileName = Path.GetFileName(path);
        return s_allowedTrustedPlatformAssemblyNames.Contains(fileName)
            || string.Equals(path, globalsAssemblyPath, StringComparison.OrdinalIgnoreCase);
    }

    private static PortableExecutableReference CreateMetadataReference(string assemblyPath)
    {
        var documentationPath = ResolveDocumentationPath(assemblyPath);
        var documentation = File.Exists(documentationPath)
            ? XmlDocumentationProvider.CreateFromFile(documentationPath)
            : DocumentationProvider.Default;

        return MetadataReference.CreateFromFile(assemblyPath, documentation: documentation);
    }

    private static string ResolveDocumentationPath(string assemblyPath)
    {
        var adjacentDocumentationPath = Path.ChangeExtension(assemblyPath, ".xml");
        if (File.Exists(adjacentDocumentationPath))
        {
            return adjacentDocumentationPath;
        }

        return ResolveReferencePackDocumentationPath(assemblyPath) ?? adjacentDocumentationPath;
    }

    private static string? ResolveReferencePackDocumentationPath(string assemblyPath)
    {
        var fileName = Path.GetFileName(assemblyPath);
        var documentationFileName = fileName.Equals("System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase)
            ? "System.Runtime.xml"
            : Path.ChangeExtension(fileName, ".xml");

        var runtimeVersion = Path.GetFileName(Path.GetDirectoryName(assemblyPath));
        var dotnetRoot = ResolveDotnetRoot(assemblyPath);
        if (string.IsNullOrWhiteSpace(runtimeVersion) || string.IsNullOrWhiteSpace(dotnetRoot))
        {
            return null;
        }

        var refPackRoot = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");
        if (!Directory.Exists(refPackRoot))
        {
            return null;
        }

        foreach (var refPackVersion in EnumerateReferencePackVersions(refPackRoot, runtimeVersion))
        {
            var documentationPath = Path.Combine(refPackVersion, "ref", $"net{GetTargetFrameworkMajor(runtimeVersion)}.0", documentationFileName);
            if (File.Exists(documentationPath))
            {
                return documentationPath;
            }
        }

        return null;
    }

    private static string? ResolveDotnetRoot(string assemblyPath)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(assemblyPath) ?? string.Empty);
        while (directory is not null)
        {
            if (directory.Name.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateReferencePackVersions(string refPackRoot, string runtimeVersion)
    {
        var runtimeMajor = GetVersionMajor(runtimeVersion);
        return Directory.EnumerateDirectories(refPackRoot)
            .Select(path => new
            {
                Path = path,
                Version = Path.GetFileName(path),
            })
            .Where(item => GetVersionMajor(item.Version) == runtimeMajor)
            .OrderByDescending(item => item.Version.Equals(runtimeVersion, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => ParseVersionPrefix(item.Version))
            .Select(item => item.Path);
    }

    private static int GetTargetFrameworkMajor(string version)
    {
        return Math.Max(1, GetVersionMajor(version));
    }

    private static int GetVersionMajor(string version)
    {
        return ParseVersionPrefix(version)?.Major ?? 0;
    }

    private static Version? ParseVersionPrefix(string version)
    {
        var versionPrefix = version.Split('-', 2)[0];
        return Version.TryParse(versionPrefix, out var parsed) ? parsed : null;
    }
}
