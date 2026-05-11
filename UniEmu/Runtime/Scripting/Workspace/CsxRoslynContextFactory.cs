using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using UniEmu.Runtime.Scripting.Environment;

namespace UniEmu.Runtime.Scripting.Workspace;

public sealed class CsxRoslynContextFactory
{
    private readonly CsxScriptEnvironment environment;
    private readonly CsxLoadedScriptExpander expander;
    private readonly MefHostServices host;

    public CsxRoslynContextFactory()
        : this(new CsxScriptEnvironment(), new CsxLoadedScriptExpander())
    {
    }

    public CsxRoslynContextFactory(CsxScriptEnvironment environment, CsxLoadedScriptExpander expander)
    {
        this.environment = environment;
        this.expander = expander;

        var assemblies = MefHostServices.DefaultAssemblies
            .Concat(
            [
                typeof(CompletionService).Assembly,
                typeof(CSharpCompilation).Assembly,
                Assembly.Load("Microsoft.CodeAnalysis.Features"),
                Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features"),
                Assembly.Load("Microsoft.CodeAnalysis.CSharp.Workspaces"),
                Assembly.Load("Microsoft.CodeAnalysis.Workspaces"),
            ])
            .Distinct();

        host = MefHostServices.Create(assemblies);
    }

    public CsxRoslynContext CreateContext(
        string entryPath,
        string content,
        int position,
        IReadOnlyDictionary<string, string> visibleScripts,
        Type globalsType)
    {
        var expanded = expander.Expand(entryPath, content, position, visibleScripts, globalsType);
        var workspace = new AdhocWorkspace(host);
        var projectId = ProjectId.CreateNewId("UniEmu.Csx");
        var documentId = DocumentId.CreateNewId(projectId, entryPath);

        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "UniEmu.Csx",
                "UniEmu.Csx",
                LanguageNames.CSharp,
                parseOptions: environment.ParseOptions,
                compilationOptions: environment.CompilationOptions,
                metadataReferences: environment.CreateMetadataReferences(globalsType)))
            .AddDocument(documentId, Path.GetFileName(entryPath), SourceText.From(expanded.Content));

        if (!workspace.TryApplyChanges(solution))
        {
            workspace.Dispose();
            throw new InvalidOperationException("Unable to apply CSX Roslyn solution changes.");
        }

        var document = workspace.CurrentSolution.GetDocument(documentId)
            ?? throw new InvalidOperationException("CSX Roslyn document was not created.");

        return new CsxRoslynContext(workspace, document, expanded.Position);
    }
}
