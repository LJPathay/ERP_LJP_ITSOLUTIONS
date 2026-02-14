using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ljp_itsolutions.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInventoryLogSchemaCorrected : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix Schema Mismatch for InventoryLogs
            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "InventoryLogs",
                newName: "LogDate");

            migrationBuilder.AddColumn<string>(
                name: "ChangeType",
                table: "InventoryLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Legacy");

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "InventoryLogs",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                columns: new[] { "CreatedAt" },
                values: new object[] { DateTime.Now });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                columns: new[] { "CreatedAt", "Password" },
                values: new object[] { new DateTime(2026, 2, 12, 23, 33, 12, 209, DateTimeKind.Local).AddTicks(7000), "AQAAAAIAAYagAAAAEEmhXNnUvV8p+L1p0v7wXv9XwQyGZG/0T0T0T0T0T0T0T0T0T0T0T0T0T0T0T0==" });
        }
    }
}
