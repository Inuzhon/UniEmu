using Microsoft.CodeAnalysis.CSharp.Scripting;
using UniEmu.Runtime;

var tests = new (string Name, Action Test)[]
{
    ("compiled script cache reuses same script for identical content", CompiledScriptCacheReusesSameScript),
    ("compiled script cache recompiles when loaded script content changes", CompiledScriptCacheRecompilesWhenDependencyChanges),
};

foreach (var test in tests)
{
    try
    {
        test.Test();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
        Environment.ExitCode = 1;
    }
}

static void CompiledScriptCacheReusesSameScript()
{
    var cache = new CompiledTagScriptCache(capacity: 8);
    var options = CSharpScript.Create<object?>("return 0;").Options;
    var visibleScripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["common.csx"] = "int Add(int a, int b) => a + b;",
    };

    var first = cache.GetOrAdd("inline/tag-1.csx", "#load \"common.csx\"\nreturn Add(1, 2);", visibleScripts, options, typeof(object));
    var second = cache.GetOrAdd("inline/tag-1.csx", "#load \"common.csx\"\nreturn Add(1, 2);", visibleScripts, options, typeof(object));

    Assert(ReferenceEquals(first, second), "Expected the same compiled Script instance for identical inputs.");
}

static void CompiledScriptCacheRecompilesWhenDependencyChanges()
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

    var first = cache.GetOrAdd("inline/tag-1.csx", "#load \"common.csx\"\nreturn Add(1, 2);", firstScripts, options, typeof(object));
    var second = cache.GetOrAdd("inline/tag-1.csx", "#load \"common.csx\"\nreturn Add(1, 2);", changedScripts, options, typeof(object));

    Assert(!ReferenceEquals(first, second), "Expected a new compiled Script when a loaded dependency changes.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
