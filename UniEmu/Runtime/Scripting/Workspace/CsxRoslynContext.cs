using Microsoft.CodeAnalysis;

namespace UniEmu.Runtime.Scripting.Workspace;

public sealed class CsxRoslynContext : IDisposable
{
    private readonly AdhocWorkspace workspace;
    private bool disposed;

    public CsxRoslynContext(AdhocWorkspace workspace, Document document, int position)
    {
        this.workspace = workspace;
        Document = document;
        Position = position;
    }

    public Document Document { get; }

    public int Position { get; }

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
