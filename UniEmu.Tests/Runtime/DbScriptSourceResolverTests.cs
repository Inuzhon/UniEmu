using System.Text;
using UniEmu.Runtime;

namespace UniEmu.Tests.Runtime;

public sealed class DbScriptSourceResolverTests
{
    [Theory]
    [InlineData("./common.csx", "common.csx")]
    [InlineData(@"folder\common.csx", "folder/common.csx")]
    [InlineData("  ./folder/common.csx  ", "folder/common.csx")]
    public void Normalize_RemovesLocalPrefixAndUsesForwardSlashes(string path, string expected)
    {
        var normalized = TagScriptPath.Normalize(path);

        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void ResolveReference_ResolvesPathRelativeToBaseScript()
    {
        var resolver = new DbScriptSourceResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["scripts/math/common.csx"] = "int Add(int a, int b) => a + b;",
        });

        var resolved = resolver.ResolveReference("common.csx", "scripts/math/main.csx");

        Assert.Equal("scripts/math/common.csx", resolved);
    }

    [Fact]
    public async Task OpenRead_ReturnsUtf8ScriptContent_ForResolvedPath()
    {
        var resolver = new DbScriptSourceResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["common.csx"] = "string Message = \"hello\";",
        });

        await using var stream = resolver.OpenRead("common.csx");
        using var reader = new StreamReader(stream, Encoding.UTF8);

        Assert.Equal("string Message = \"hello\";", await reader.ReadToEndAsync());
    }

    [Fact]
    public void OpenRead_ThrowsFileNotFoundException_WhenScriptIsMissing()
    {
        var resolver = new DbScriptSourceResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var exception = Assert.Throws<FileNotFoundException>(() => resolver.OpenRead("missing.csx"));

        Assert.Contains("missing.csx", exception.Message, StringComparison.Ordinal);
    }
}
