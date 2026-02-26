using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ljp_itsolutions.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetAudienceToPromotion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetAudience",
                table: "Promotions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "PromotionID",
                keyValue: 1,
                column: "TargetAudience",
                value: "All Patrons");

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "PromotionID",
                keyValue: 2,
                column: "TargetAudience",
                value: "All Patrons");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 10, 57, 36, 17, DateTimeKind.Local).AddTicks(4255));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetAudience",
                table: "Promotions");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 25, 17, 24, 21, 559, DateTimeKind.Local).AddTicks(8920));
        }
    }
}
