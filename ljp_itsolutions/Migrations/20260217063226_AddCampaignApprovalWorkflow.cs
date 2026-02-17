using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ljp_itsolutions.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "Promotions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ApprovedBy",
                table: "Promotions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedDate",
                table: "Promotions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Promotions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "PromotionID",
                keyValue: 1,
                columns: new[] { "ApprovalStatus", "ApprovedBy", "ApprovedDate", "RejectionReason" },
                values: new object[] { "Pending", null, null, null });

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "PromotionID",
                keyValue: 2,
                columns: new[] { "ApprovalStatus", "ApprovedBy", "ApprovedDate", "RejectionReason" },
                values: new object[] { "Pending", null, null, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 32, 23, 927, DateTimeKind.Local).AddTicks(8565));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "ApprovedDate",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Promotions");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 13, 23, 18, 268, DateTimeKind.Local).AddTicks(2578));
        }
    }
}
