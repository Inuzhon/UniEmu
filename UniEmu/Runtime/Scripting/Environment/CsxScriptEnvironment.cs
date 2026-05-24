using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Scripting;
using UniEmu.Scripting.Api;

namespace UniEmu.Runtime.Scripting.Environment;

/// <summary>
/// Описывает ограниченное окружение Roslyn для компиляции, анализа и выполнения пользовательских CSX-скриптов.
/// </summary>
public sealed class CsxScriptEnvironment
{
    /// <summary>
    /// Имена сборок платформы, которые разрешено добавлять в набор ссылок скрипта.
    /// </summary>
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
        "System.Text.Encoding.dll",
        "System.Text.Encoding.Extensions.dll",
        "System.Threading.dll",
        "System.Threading.Tasks.dll",
        "UniEmu.Scripting.Api.dll",
    };

    /// <summary>
    /// Пространства имен, автоматически импортируемые во все пользовательские скрипты.
    /// </summary>
    private static readonly string[] s_imports =
    [
        "System",
        "System.Collections.Generic",
        "System.Globalization",
        "System.Linq",
        "UniEmu.Scripting.Api",
    ];

    /// <summary>
    /// Кэш наборов metadata reference по типу globals, чтобы не пересобирать ссылки для каждого запроса анализа.
    /// </summary>
    private readonly ConcurrentDictionary<Type, IReadOnlyList<MetadataReference>> metadataReferenceCache = new();

    /// <summary>
    /// Параметры разбора C#-кода в режиме script с preview-версией языка и диагностикой XML-документации.
    /// </summary>
    public CSharpParseOptions ParseOptions { get; } = CSharpParseOptions.Default
        .WithKind(SourceCodeKind.Script)
        .WithLanguageVersion(LanguageVersion.Preview)
        .WithDocumentationMode(DocumentationMode.Diagnose);

    /// <summary>
    /// Параметры компиляции скрипта с запрещенным unsafe-кодом и преднастроенными импортами.
    /// </summary>
    public CSharpCompilationOptions CompilationOptions { get; } = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        .WithUsings(s_imports)
        .WithAllowUnsafe(false)
        .WithOptimizationLevel(OptimizationLevel.Debug)
        .WithNullableContextOptions(NullableContextOptions.Enable);

    /// <summary>
    /// Создает параметры Roslyn scripting для входного файла и доступных ему скриптов.
    /// </summary>
    /// <param name="entryPath">Путь входного CSX-файла, используемый для диагностики и относительных <c>#load</c>.</param>
    /// <param name="visibleScripts">Скрипты, доступные текущему эмулятору или общей области видимости.</param>
    /// <param name="globalsType">Тип globals-объекта, через который скрипт получает публичный API.</param>
    /// <returns>Параметры выполнения и компиляции CSX-скрипта.</returns>
    public ScriptOptions CreateScriptOptions(
        string entryPath,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type? globalsType = null)
    {
        return ScriptOptions.Default
            .WithReferences(CreateMetadataReferences(globalsType ?? typeof(TagScriptGlobals)))
            .WithImports(s_imports)
            .WithFilePath(TagScriptPath.Normalize(entryPath))
            .WithSourceResolver(new DbScriptSourceResolver(CsxNullableContext.ApplyToScripts(visibleScripts)));
    }

    /// <summary>
    /// Возвращает компактный набор ссылок, необходимых для анализа и выполнения скрипта.
    /// </summary>
    /// <param name="globalsType">Тип globals-объекта, сборка которого должна быть доступна скрипту.</param>
    /// <returns>Список metadata reference с документацией, если она найдена.</returns>
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

    /// <summary>
    /// Возвращает количество закэшированных наборов metadata reference.
    /// </summary>
    internal int MetadataReferenceCacheCount => metadataReferenceCache.Count;

    /// <summary>
    /// Очищает кэш metadata reference для изолированных тестов.
    /// </summary>
    internal void ClearMetadataReferenceCacheForTests()
    {
        metadataReferenceCache.Clear();
    }

    /// <summary>
    /// Собирает разрешенные ссылки из TRUSTED_PLATFORM_ASSEMBLIES и добавляет сборку globals-типа.
    /// </summary>
    /// <param name="globalsType">Тип globals-объекта, сборка которого нужна скрипту.</param>
    /// <returns>Список разрешенных ссылок платформы и scripting API.</returns>
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

    /// <summary>
    /// Проверяет, можно ли добавить сборку из доверенного списка платформы в окружение скрипта.
    /// </summary>
    /// <param name="path">Полный путь к сборке платформы.</param>
    /// <param name="globalsAssemblyPath">Полный путь к сборке globals-типа.</param>
    /// <returns><see langword="true"/>, если сборка входит в разрешенный набор.</returns>
    private static bool IsAllowedTrustedPlatformAssembly(string path, string globalsAssemblyPath)
    {
        var fileName = Path.GetFileName(path);
        return s_allowedTrustedPlatformAssemblyNames.Contains(fileName)
            || string.Equals(path, globalsAssemblyPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Создает metadata reference для сборки и подключает XML-документацию при наличии.
    /// </summary>
    /// <param name="assemblyPath">Полный путь к сборке.</param>
    /// <returns>Ссылка Roslyn на указанную сборку.</returns>
    private static PortableExecutableReference CreateMetadataReference(string assemblyPath)
    {
        var documentationPath = ResolveDocumentationPath(assemblyPath);
        var documentation = File.Exists(documentationPath)
            ? XmlDocumentationProvider.CreateFromFile(documentationPath)
            : DocumentationProvider.Default;

        return MetadataReference.CreateFromFile(assemblyPath, documentation: documentation);
    }

    /// <summary>
    /// Определяет путь к XML-документации для указанной сборки.
    /// </summary>
    /// <param name="assemblyPath">Полный путь к сборке.</param>
    /// <returns>Существующий или ожидаемый путь к XML-документации.</returns>
    private static string ResolveDocumentationPath(string assemblyPath)
    {
        var adjacentDocumentationPath = Path.ChangeExtension(assemblyPath, ".xml");
        if (File.Exists(adjacentDocumentationPath))
        {
            return adjacentDocumentationPath;
        }

        return ResolveReferencePackDocumentationPath(assemblyPath) ?? adjacentDocumentationPath;
    }

    /// <summary>
    /// Ищет XML-документацию сборки в reference pack текущего .NET runtime.
    /// </summary>
    /// <param name="assemblyPath">Полный путь к runtime-сборке.</param>
    /// <returns>Путь к XML-документации или <see langword="null"/>, если найти ее не удалось.</returns>
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

    /// <summary>
    /// Находит корневую директорию установки .NET по пути к runtime-сборке.
    /// </summary>
    /// <param name="assemblyPath">Полный путь к сборке внутри установки .NET.</param>
    /// <returns>Путь к корню .NET или <see langword="null"/>, если он не найден.</returns>
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

    /// <summary>
    /// Перечисляет подходящие версии reference pack, начиная с наиболее близкой к runtime-версии.
    /// </summary>
    /// <param name="refPackRoot">Корневая директория reference pack.</param>
    /// <param name="runtimeVersion">Версия runtime, из которой взята сборка.</param>
    /// <returns>Пути к кандидатам reference pack.</returns>
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

    /// <summary>
    /// Возвращает major-версию целевого framework для пути reference pack.
    /// </summary>
    /// <param name="version">Строка версии runtime или reference pack.</param>
    /// <returns>Major-версия target framework.</returns>
    private static int GetTargetFrameworkMajor(string version)
    {
        return Math.Max(1, GetVersionMajor(version));
    }

    /// <summary>
    /// Извлекает major-версию из строки версии.
    /// </summary>
    /// <param name="version">Строка версии.</param>
    /// <returns>Major-версия или 0, если строка не распознана.</returns>
    private static int GetVersionMajor(string version)
    {
        return ParseVersionPrefix(version)?.Major ?? 0;
    }

    /// <summary>
    /// Разбирает числовой префикс версии без prerelease-суффикса.
    /// </summary>
    /// <param name="version">Строка версии.</param>
    /// <returns>Разобранная версия или <see langword="null"/>.</returns>
    private static Version? ParseVersionPrefix(string version)
    {
        var versionPrefix = version.Split('-', 2)[0];
        return Version.TryParse(versionPrefix, out var parsed) ? parsed : null;
    }
}
