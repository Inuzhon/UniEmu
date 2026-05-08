using Microsoft.EntityFrameworkCore;

namespace UniEmu.Data;

public static class UniEmuSchemaUpdater
{
    public static async Task ApplyCompatibilityUpdatesAsync(UniEmuDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await HasColumnAsync(db, "Emulators", "ProtocolId", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Emulators ADD COLUMN ProtocolId INTEGER NOT NULL DEFAULT 0",
                cancellationToken);
        }

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE Emulators
            SET ProtocolId = CAST(REPLACE(Id, 'em-', '') AS INTEGER)
            WHERE ProtocolId = 0 AND Id LIKE 'em-%'
            """,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS ScriptRuntimeStates (
                Id TEXT NOT NULL CONSTRAINT PK_ScriptRuntimeStates PRIMARY KEY,
                EmulatorId TEXT NOT NULL,
                ScriptKey TEXT NOT NULL,
                ValuesJson TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )
            """,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_ScriptRuntimeStates_EmulatorId_ScriptKey
            ON ScriptRuntimeStates (EmulatorId, ScriptKey)
            """,
            cancellationToken);
    }

    private static async Task<bool> HasColumnAsync(
        UniEmuDbContext db,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State == System.Data.ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName})";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
