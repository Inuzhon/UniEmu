using Microsoft.CodeAnalysis;

namespace UniEmu.Runtime.Scripting.Workspace;

public sealed class CsxRoslynContext : IDisposable
{
    private readonly AdhocWorkspace workspace;
    private bool disposed;

    public CsxRoslynContext(
        AdhocWorkspace workspace,
        Document document,
        string entryPath,
        string entryContent,
        int position,
        int entryContentStart)
    {
        this.workspace = workspace;
        Document = document;
        EntryPath = entryPath;
        EntryContent = entryContent;
        Position = position;
        EntryContentStart = entryContentStart;
    }

    public Document Document { get; }

    public string EntryPath { get; }

    public string EntryContent { get; }

    public int Position { get; }

    public int EntryContentStart { get; }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        workspace.Dispose();
        disposed = true;
    }
}
