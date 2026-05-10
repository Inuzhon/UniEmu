using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniEmu.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Emulators",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ProtocolId = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    IntervalSec = table.Column<int>(type: "INTEGER", nullable: false),
                    LastRun = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    NextRun = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    TotalRequests = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Emulators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CncPrograms",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    EmulatorId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    SizeBytes = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsBinary = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CncPrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CncPrograms_Emulators_EmulatorId",
                        column: x => x.EmulatorId,
                        principalTable: "Emulators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmulatorTags",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EmulatorId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Preview = table.Column<string>(type: "TEXT", nullable: false),
                    TriggerJson = table.Column<string>(type: "TEXT", nullable: false),
                    CalcJson = table.Column<string>(type: "TEXT", nullable: true),
                    FormulaJson = table.Column<string>(type: "TEXT", nullable: true),
                    ScenarioJson = table.Column<string>(type: "TEXT", nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    RoundDigits = table.Column<int>(type: "INTEGER", nullable: true),
                    SpecialParameter = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmulatorTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmulatorTags_Emulators_EmulatorId",
                        column: x => x.EmulatorId,
                        principalTable: "Emulators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScriptFiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    EmulatorId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SizeBytes = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScriptFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScriptFiles_Emulators_EmulatorId",
                        column: x => x.EmulatorId,
                        principalTable: "Emulators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScriptRuntimeStates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EmulatorId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ScriptKey = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    ValuesJson = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScriptRuntimeStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScriptRuntimeStates_Emulators_EmulatorId",
                        column: x => x.EmulatorId,
                        principalTable: "Emulators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EmulatorId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EmulatorName = table.Column<string>(type: "TEXT", nullable: false),
                    Level = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemEvents_Emulators_EmulatorId",
                        column: x => x.EmulatorId,
                        principalTable: "Emulators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryPoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmulatorId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ValuesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelemetryPoints_Emulators_EmulatorId",
                        column: x => x.EmulatorId,
                        principalTable: "Emulators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CncPrograms_EmulatorId",
                table: "CncPrograms",
                column: "EmulatorId");

            migrationBuilder.CreateIndex(
                name: "IX_CncPrograms_Scope_EmulatorId_Name",
                table: "CncPrograms",
                columns: new[] { "Scope", "EmulatorId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmulatorTags_EmulatorId",
                table: "EmulatorTags",
                column: "EmulatorId");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptFiles_EmulatorId",
                table: "ScriptFiles",
                column: "EmulatorId");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptFiles_Scope_EmulatorId_Name",
                table: "ScriptFiles",
                columns: new[] { "Scope", "EmulatorId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScriptRuntimeStates_EmulatorId_ScriptKey",
                table: "ScriptRuntimeStates",
                columns: new[] { "EmulatorId", "ScriptKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_EmulatorId",
                table: "SystemEvents",
                column: "EmulatorId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_Timestamp",
                table: "SystemEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryPoints_EmulatorId_Timestamp",
                table: "TelemetryPoints",
                columns: new[] { "EmulatorId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CncPrograms");

            migrationBuilder.DropTable(
                name: "EmulatorTags");

            migrationBuilder.DropTable(
                name: "ScriptFiles");

            migrationBuilder.DropTable(
                name: "ScriptRuntimeStates");

            migrationBuilder.DropTable(
                name: "SystemEvents");

            migrationBuilder.DropTable(
                name: "TelemetryPoints");

            migrationBuilder.DropTable(
                name: "Emulators");
        }
    }
}
