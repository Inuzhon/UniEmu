using System.Text.RegularExpressions;

namespace UniEmu.Tests.Runtime.Scripting;

public sealed class CsxRestReplacementSourceTests
{
    [Fact]
    public async Task Program_MapsControllersInsteadOfCsxLspEndpoint()
    {
        var source = await File.ReadAllTextAsync(ProjectPath("UniEmu", "Program.cs"));

        Assert.Contains("app.MapControllers();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("app.MapCsxLsp();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UseWebSockets", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Backend_ExposesCsharpRestIntellisenseRoutes()
    {
        var source = await File.ReadAllTextAsync(ProjectPath("UniEmu", "Controllers", "IntellisenseController.cs"));

        Assert.Contains("[Route(\"api/intellisense/csharp\")]", source, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"\[HttpPost\(""diagnostics""\)\]"), source);
        Assert.Matches(new Regex(@"\[HttpPost\(""completions""\)\]"), source);
        Assert.Matches(new Regex(@"\[HttpPost\(""hover""\)\]"), source);
        Assert.Matches(new Regex(@"\[HttpPost\(""signature-help""\)\]"), source);
    }

    private static string ProjectPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UniEmu.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. parts]);
    }
}
