using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        private static readonly string[] AlarmIndexColumns = ["MachineId", "AcknowledgedAtUtc", "CreatedAtUtc"];
        private static readonly string[] ReadingIndexColumns = ["MachineId", "TimestampUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Machines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MachineCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    MachineType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                    table.CheckConstraint("CK_Machines_Code", "length(\"MachineCode\") BETWEEN 1 AND 32 AND \"MachineCode\" NOT GLOB '*[^A-Z0-9_-]*'");
                    table.CheckConstraint("CK_Machines_Metadata", "length(trim(\"Name\")) BETWEEN 1 AND 100 AND length(trim(\"MachineType\")) BETWEEN 1 AND 50 AND length(trim(\"Location\")) BETWEEN 1 AND 100 AND (\"Description\" IS NULL OR length(trim(\"Description\")) BETWEEN 1 AND 500)");
                    table.CheckConstraint("CK_Machines_Status", "\"Status\" IN (0, 1, 2, 3)");
                    table.CheckConstraint("CK_Machines_TimestampOrder", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                });

            migrationBuilder.CreateTable(
                name: "MachineAlarms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AlarmCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    AcknowledgedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineAlarms", x => x.Id);
                    table.CheckConstraint("CK_MachineAlarms_AcknowledgementOrder", "\"AcknowledgedAtUtc\" IS NULL OR \"AcknowledgedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_MachineAlarms_Severity", "\"Severity\" IN (0, 1, 2)");
                    table.CheckConstraint("CK_MachineAlarms_Text", "length(trim(\"AlarmCode\")) BETWEEN 1 AND 50 AND length(trim(\"Message\")) BETWEEN 1 AND 500");
                    table.ForeignKey(
                        name: "FK_MachineAlarms_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MachineReadings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TimestampUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Temperature = table.Column<double>(type: "REAL", nullable: false),
                    Vibration = table.Column<double>(type: "REAL", nullable: false),
                    Rpm = table.Column<int>(type: "INTEGER", nullable: false),
                    Torque = table.Column<double>(type: "REAL", nullable: false),
                    ToolWear = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineReadings", x => x.Id);
                    table.CheckConstraint("CK_MachineReadings_MeasurementRange", "\"Vibration\" >= 0 AND \"Rpm\" >= 0 AND \"ToolWear\" BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_MachineReadings_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MachineAlarms_MachineId_AcknowledgedAtUtc_CreatedAtUtc",
                table: "MachineAlarms",
                columns: AlarmIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_MachineReadings_MachineId_TimestampUtc",
                table: "MachineReadings",
                columns: ReadingIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Machines_MachineCode",
                table: "Machines",
                column: "MachineCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MachineAlarms");

            migrationBuilder.DropTable(
                name: "MachineReadings");

            migrationBuilder.DropTable(
                name: "Machines");
        }
    }
}
