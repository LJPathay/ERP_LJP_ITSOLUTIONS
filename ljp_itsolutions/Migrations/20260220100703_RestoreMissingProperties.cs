using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ljp_itsolutions.Migrations
{
    /// <inheritdoc />
    public partial class RestoreMissingProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryLogs_Products_ProductID",
                table: "InventoryLogs");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "Promotions");
 
            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedBy",
                table: "Promotions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Remarks",
                table: "InventoryLogs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityChange",
                table: "InventoryLogs",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
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

/*
            migrationBuilder.AddColumn<int>(
                name: "IngredientID",
                table: "InventoryLogs",
                type: "int",
                nullable: true);
*/

/*
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
*/

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Expenses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

/*
            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);
*/

            migrationBuilder.UpdateData(
                table: "Expenses",
                keyColumn: "ExpenseID",
                keyValue: 1,
                column: "CreatedBy",
                value: null);

            migrationBuilder.UpdateData(
                table: "Expenses",
                keyColumn: "ExpenseID",
                keyValue: 2,
                column: "CreatedBy",
                value: null);

            migrationBuilder.UpdateData(
                table: "Expenses",
                keyColumn: "ExpenseID",
                keyValue: 3,
                column: "CreatedBy",
                value: null);

/*
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
*/

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "PromotionID",
                keyValue: 1,
                column: "ApprovedBy",
                value: null);

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "PromotionID",
                keyValue: 2,
                column: "ApprovedBy",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 20, 18, 7, 1, 934, DateTimeKind.Local).AddTicks(5810));

/*
            migrationBuilder.CreateIndex(
                name: "IX_InventoryLogs_IngredientID",
                table: "InventoryLogs",
                column: "IngredientID");
*/

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CreatedBy",
                table: "Expenses",
                column: "CreatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Users_CreatedBy",
                table: "Expenses",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "UserID");

/*
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
*/
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Users_CreatedBy",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryLogs_Ingredients_IngredientID",
                table: "InventoryLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryLogs_Products_ProductID",
                table: "InventoryLogs");

            migrationBuilder.DropIndex(
                name: "IX_InventoryLogs_IngredientID",
                table: "InventoryLogs");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_CreatedBy",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "IngredientID",
                table: "InventoryLogs");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "LastStockedDate",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<int>(
                name: "ApprovedBy",
                table: "Promotions",
                type: "int",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Remarks",
                table: "InventoryLogs",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "QuantityChange",
                table: "InventoryLogs",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

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
                table: "Promotions",
                keyColumn: "PromotionID",
                keyValue: 1,
                column: "ApprovedBy",
                value: null);

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "PromotionID",
                keyValue: 2,
                column: "ApprovedBy",
                value: null);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 18, 22, 16, 27, 718, DateTimeKind.Local).AddTicks(4669));

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
