using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ljp_itsolutions.Migrations
{
    /// <inheritdoc />
    public partial class AddIsArchivedFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Ingredients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Ingredients",
                keyColumn: "IngredientID",
                keyValue: 1,
                column: "IsArchived",
                value: false);

            migrationBuilder.UpdateData(
                table: "Ingredients",
                keyColumn: "IngredientID",
                keyValue: 2,
                column: "IsArchived",
                value: false);

            migrationBuilder.UpdateData(
                table: "Ingredients",
                keyColumn: "IngredientID",
                keyValue: 3,
                column: "IsArchived",
                value: false);

            migrationBuilder.UpdateData(
                table: "Ingredients",
                keyColumn: "IngredientID",
                keyValue: 4,
                column: "IsArchived",
                value: false);

            migrationBuilder.UpdateData(
                table: "Ingredients",
                keyColumn: "IngredientID",
                keyValue: 5,
                column: "IsArchived",
                value: false);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: 1,
                column: "IsArchived",
                value: false);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: 2,
                column: "IsArchived",
                value: false);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: 3,
                column: "IsArchived",
                value: false);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: 4,
                column: "IsArchived",
                value: false);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: 5,
                column: "IsArchived",
                value: false);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 20, 41, 20, 947, DateTimeKind.Local).AddTicks(6839));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Ingredients");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 10, 57, 36, 17, DateTimeKind.Local).AddTicks(4255));
        }
    }
}
