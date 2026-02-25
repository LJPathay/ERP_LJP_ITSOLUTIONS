using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ljp_itsolutions.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionRedemptionControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOneTimeReward",
                table: "Promotions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxRedemptions",
                table: "Promotions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OneTimePerCustomer",
                table: "Promotions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "PromotionID",
                keyValue: 1,
                columns: new[] { "IsOneTimeReward", "MaxRedemptions", "OneTimePerCustomer" },
                values: new object[] { false, null, false });

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "PromotionID",
                keyValue: 2,
                columns: new[] { "IsOneTimeReward", "MaxRedemptions", "OneTimePerCustomer" },
                values: new object[] { false, null, false });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 25, 17, 24, 21, 559, DateTimeKind.Local).AddTicks(8920));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOneTimeReward",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "MaxRedemptions",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "OneTimePerCustomer",
                table: "Promotions");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 24, 23, 25, 41, 336, DateTimeKind.Local).AddTicks(1885));
        }
    }
}
