using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlarmLifecycle : Migration
    {
        private static readonly string[] ActiveAlarmColumns = ["MachineId", "AlarmCode"];
        private static readonly string[] MachineHistoryColumns = ["MachineId", "CreatedAtUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "MachineAlarms" ADD COLUMN "ResolvedAtUtc" INTEGER NULL
                    CONSTRAINT "CK_MachineAlarms_ResolutionOrder"
                    CHECK ("ResolvedAtUtc" IS NULL OR "ResolvedAtUtc" >= "CreatedAtUtc");
                """);

            // Keep the newest legacy episode active and preserve earlier rows as history.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (PARTITION BY "MachineId", "AlarmCode"
                                              ORDER BY "CreatedAtUtc" DESC, "Id" DESC) AS position,
                           MAX("CreatedAtUtc") OVER (PARTITION BY "MachineId", "AlarmCode") AS newest
                    FROM "MachineAlarms"
                )
                UPDATE "MachineAlarms" AS alarm
                SET "ResolvedAtUtc" = MAX(COALESCE(alarm."AcknowledgedAtUtc", alarm."CreatedAtUtc"), ranked.newest)
                FROM ranked
                WHERE alarm."Id" = ranked."Id" AND ranked.position > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_MachineAlarms_Active_CreatedAtUtc",
                table: "MachineAlarms",
                column: "CreatedAtUtc",
                filter: "\"ResolvedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MachineAlarms_CreatedAtUtc",
                table: "MachineAlarms",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MachineAlarms_MachineId_AlarmCode",
                table: "MachineAlarms",
                columns: ActiveAlarmColumns,
                unique: true,
                filter: "\"ResolvedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MachineAlarms_MachineId_CreatedAtUtc",
                table: "MachineAlarms",
                columns: MachineHistoryColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MachineAlarms_Active_CreatedAtUtc",
                table: "MachineAlarms");

            migrationBuilder.DropIndex(
                name: "IX_MachineAlarms_CreatedAtUtc",
                table: "MachineAlarms");

            migrationBuilder.DropIndex(
                name: "IX_MachineAlarms_MachineId_AlarmCode",
                table: "MachineAlarms");

            migrationBuilder.DropIndex(
                name: "IX_MachineAlarms_MachineId_CreatedAtUtc",
                table: "MachineAlarms");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MachineAlarms_ResolutionOrder",
                table: "MachineAlarms");

            migrationBuilder.DropColumn(
                name: "ResolvedAtUtc",
                table: "MachineAlarms");
        }
    }
}
