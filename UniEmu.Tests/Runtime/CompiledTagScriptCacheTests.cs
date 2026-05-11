using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using UniEmu.Runtime;

namespace UniEmu.Tests.Runtime;

public sealed class CompiledTagScriptCacheTests
{
    [Fact]
    public void GetOrAdd_ReturnsSameCompiledScript_WhenInputsAreIdentical()
    {
        var cache = new CompiledTagScriptCache(capacity: 8);
        var options = CSharpScript.Create<object?>("return 0;").Options;
        var visibleScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["common.csx"] = "int Add(int a, int b) => a + b;",
        };

        var first = cache.GetOrAdd(
            "inline/tag-1.csx",
            "#load \"common.csx\"\nreturn Add(1, 2);",
            visibleScripts,
            options,
            typeof(object));
        var second = cache.GetOrAdd(
            "inline/tag-1.csx",
            "#load \"common.csx\"\nreturn Add(1, 2);",
            visibleScripts,
            options,
            typeof(object));

        Assert.Same(first, second);
    }

    [Fact]
    public void GetOrAdd_RecompilesScript_WhenLoadedDependencyContentChanges()
    {
        var cache = new CompiledTagScriptCache(capacity: 8);
        var options = CSharpScript.Create<object?>("return 0;").Options;
        var firstScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["common.csx"] = "int Add(int a, int b) => a + b;",
        };
        var changedScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["common.csx"] = "int Add(int a, int b) => a + b + 1;",
        };

        var first = cache.GetOrAdd(
            "inline/tag-1.csx",
            "#load \"common.csx\"\nreturn Add(1, 2);",
            firstScripts,
            options,
            typeof(object));
        var second = cache.GetOrAdd(
            "inline/tag-1.csx",
            "#load \"common.csx\"\nreturn Add(1, 2);",
            changedScripts,
            options,
            typeof(object));

        Assert.NotSame(first, second);
    }

    [Fact]
    public void GetOrAdd_CompilesScript_WhenLoadDirectiveUsesPathRelativeToEntryPath()
    {
        var cache = new CompiledTagScriptCache(capacity: 8);
        var options = CSharpScript.Create<object?>("return 0;").Options;
        var visibleScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags/shared/math.csx"] = "int Add(int a, int b) => a + b;",
        };

        var script = cache.GetOrAdd(
            "tags/tag-1.csx",
            "#load \"shared/math.csx\"\nreturn Add(1, 2);",
            visibleScripts,
            options,
            typeof(object));

        Assert.Empty(script.Compile().Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
    }

    [Fact]
    public void GetOrAdd_EvictsLeastRecentlyUsedScript_WhenCapacityIsExceeded()
    {
        var cache = new CompiledTagScriptCache(capacity: 2);
        var options = CSharpScript.Create<object?>("return 0;").Options;
        var scripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var first = cache.GetOrAdd("one.csx", "return 1;", scripts, options, typeof(object));
        _ = cache.GetOrAdd("two.csx", "return 2;", scripts, options, typeof(object));
        _ = cache.GetOrAdd("three.csx", "return 3;", scripts, options, typeof(object));

        var recompiledFirst = cache.GetOrAdd("one.csx", "return 1;", scripts, options, typeof(object));

        Assert.NotSame(first, recompiledFirst);
    }

    [Fact]
    public void Clear_RemovesCompiledScripts()
    {
        var cache = new CompiledTagScriptCache(capacity: 8);
        var options = CSharpScript.Create<object?>("return 0;").Options;
        var scripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var first = cache.GetOrAdd("one.csx", "return 1;", scripts, options, typeof(object));

        cache.Clear();

        Assert.Equal(0, cache.Count);
        var recompiled = cache.GetOrAdd("one.csx", "return 1;", scripts, options, typeof(object));
        Assert.NotSame(first, recompiled);
    }

    [Fact]
    public void GetOrAdd_ThrowsCompilationErrorException_WhenScriptDoesNotCompile()
    {
        var cache = new CompiledTagScriptCache(capacity: 8);
        var options = CSharpScript.Create<object?>("return 0;").Options;
        var scripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var exception = Assert.Throws<CompilationErrorException>(() =>
            cache.GetOrAdd(
                "broken.csx",
                "return MissingValue;",
                scripts,
                options,
                typeof(object)));

        Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Id == "CS0103");
    }
}
