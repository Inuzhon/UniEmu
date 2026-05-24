using UniEmu.Runtime.Scripting;

namespace UniEmu.Tests.Runtime.Scripting;

public sealed class TagScriptContentNormalizerTests
{
    [Fact]
    public void NormalizeEntryScriptContent_DoesNotRewriteInnerReturn_WhenFinalReturnIsMultiline()
    {
        const string content =
            """
            if (phase == "Transfer")
                return "Released";

            return phase == "Reaction"
                ? "InSpec"
                : "Pending";
            """;

        var normalized = TagScriptContentNormalizer.NormalizeEntryScriptContent(content);

        Assert.Equal(content, normalized);
    }

    [Fact]
    public void NormalizeEntryScriptContent_RewritesFinalSingleLineReturn()
    {
        const string content =
            """
            var value = 41;
            return value + 1;
            """;

        var normalized = TagScriptContentNormalizer.NormalizeEntryScriptContent(content);

        Assert.DoesNotContain("return value + 1;", normalized);
        Assert.EndsWith("value + 1", normalized);
    }
}
