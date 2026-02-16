using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ljp_itsolutions.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessibilitySettingsToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FontSize",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "default");

            migrationBuilder.AddColumn<bool>(
                name: "IsHighContrast",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReduceMotion",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                columns: new[] { "CreatedAt", "FontSize" },
                values: new object[] { new DateTime(2026, 2, 16, 17, 16, 59, 936, DateTimeKind.Local).AddTicks(2324), "default" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FontSize",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsHighContrast",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ReduceMotion",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 14, 12, 42, 7, 177, DateTimeKind.Local).AddTicks(2802));
        }
    }
}
