using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniEmu.Data.Migrations
{
    /// <inheritdoc />
    public partial class SharedResourcePartialUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScriptFiles_EmulatorId",
                table: "ScriptFiles");

            migrationBuilder.DropIndex(
                name: "IX_CncPrograms_EmulatorId",
                table: "CncPrograms");

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_ScriptFiles_EmulatorId_Name"
                ON "ScriptFiles" ("EmulatorId", "Name" COLLATE NOCASE)
                WHERE Scope = 'emulator' AND EmulatorId IS NOT NULL
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_ScriptFiles_Shared_Name"
                ON "ScriptFiles" ("Name" COLLATE NOCASE)
                WHERE Scope = 'shared' AND EmulatorId IS NULL
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_CncPrograms_EmulatorId_Name"
                ON "CncPrograms" ("EmulatorId", "Name" COLLATE NOCASE)
                WHERE Scope = 'emulator' AND EmulatorId IS NOT NULL
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_CncPrograms_Shared_Name"
                ON "CncPrograms" ("Name" COLLATE NOCASE)
                WHERE Scope = 'shared' AND EmulatorId IS NULL
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScriptFiles_EmulatorId_Name",
                table: "ScriptFiles");

            migrationBuilder.DropIndex(
                name: "IX_ScriptFiles_Shared_Name",
                table: "ScriptFiles");

            migrationBuilder.DropIndex(
                name: "IX_CncPrograms_EmulatorId_Name",
                table: "CncPrograms");

            migrationBuilder.DropIndex(
                name: "IX_CncPrograms_Shared_Name",
                table: "CncPrograms");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptFiles_EmulatorId",
                table: "ScriptFiles",
                column: "EmulatorId");

            migrationBuilder.CreateIndex(
                name: "IX_CncPrograms_EmulatorId",
                table: "CncPrograms",
                column: "EmulatorId");
        }
    }
}
