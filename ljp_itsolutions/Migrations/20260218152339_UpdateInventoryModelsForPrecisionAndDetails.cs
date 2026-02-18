using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ljp_itsolutions.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInventoryModelsForPrecisionAndDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryLogs_Products_ProductID",
                table: "InventoryLogs");

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityChange",
                table: "InventoryLogs",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "ProductID",
                table: "InventoryLogs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "IngredientID",
                table: "InventoryLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "Ingredients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastStockedDate",
                table: "Ingredients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Ingredients",
                keyColumn: "IngredientID",
                keyValue: 1,
                columns: new[] { "ExpiryDate", "LastStockedDate" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Ingredients",
                keyColumn: "IngredientID",
                keyValue: 2,
                columns: new[] { "ExpiryDate", "LastStockedDate" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Ingredients",
                keyColumn: "IngredientID",
                keyValue: 3,
                columns: new[] { "ExpiryDate", "LastStockedDate" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Ingredients",
                keyColumn: "IngredientID",
                keyValue: 4,
                columns: new[] { "ExpiryDate", "LastStockedDate" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Ingredients",
                keyColumn: "IngredientID",
                keyValue: 5,
                columns: new[] { "ExpiryDate", "LastStockedDate" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 18, 23, 23, 37, 647, DateTimeKind.Local).AddTicks(7022));

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLogs_IngredientID",
                table: "InventoryLogs",
                column: "IngredientID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryLogs_Ingredients_IngredientID",
                table: "InventoryLogs",
                column: "IngredientID",
                principalTable: "Ingredients",
                principalColumn: "IngredientID");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryLogs_Products_ProductID",
                table: "InventoryLogs",
                column: "ProductID",
                principalTable: "Products",
                principalColumn: "ProductID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryLogs_Ingredients_IngredientID",
                table: "InventoryLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryLogs_Products_ProductID",
                table: "InventoryLogs");

            migrationBuilder.DropIndex(
                name: "IX_InventoryLogs_IngredientID",
                table: "InventoryLogs");

            migrationBuilder.DropColumn(
                name: "IngredientID",
                table: "InventoryLogs");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "LastStockedDate",
                table: "Ingredients");

            migrationBuilder.AlterColumn<int>(
                name: "QuantityChange",
                table: "InventoryLogs",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<int>(
                name: "ProductID",
                table: "InventoryLogs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 18, 22, 52, 5, 929, DateTimeKind.Local).AddTicks(9608));

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryLogs_Products_ProductID",
                table: "InventoryLogs",
                column: "ProductID",
                principalTable: "Products",
                principalColumn: "ProductID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
