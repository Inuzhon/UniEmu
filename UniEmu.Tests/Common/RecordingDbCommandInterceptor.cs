using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace UniEmu.Tests.Common;

internal sealed class RecordingDbCommandInterceptor : DbCommandInterceptor
{
    private readonly List<string> commandTexts = [];

    public IReadOnlyList<string> CommandTexts => commandTexts;

    public void Clear() => commandTexts.Clear();

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        commandTexts.Add(command.CommandText);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        commandTexts.Add(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
