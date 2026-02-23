using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ljp_itsolutions.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationTargetUrlAndIsRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetUrl",
                table: "Notifications",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 23, 18, 35, 23, 85, DateTimeKind.Local).AddTicks(910));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetUrl",
                table: "Notifications");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 21, 11, 52, 38, 728, DateTimeKind.Local).AddTicks(9601));
        }
    }
}
