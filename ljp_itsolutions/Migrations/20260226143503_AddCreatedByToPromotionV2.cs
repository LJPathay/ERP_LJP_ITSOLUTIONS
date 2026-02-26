using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ljp_itsolutions.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedByToPromotionV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Promotions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "PromotionID",
                keyValue: 1,
                column: "CreatedBy",
                value: null);

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "PromotionID",
                keyValue: 2,
                column: "CreatedBy",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 22, 35, 1, 344, DateTimeKind.Local).AddTicks(6285));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Promotions");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 20, 41, 20, 947, DateTimeKind.Local).AddTicks(6839));
        }
    }
}
