using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ljp_itsolutions.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAndFinanceSystemsV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {


            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    ExpenseID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpenseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.ExpenseID);
                });

            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    IngredientID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StockQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LowStockThreshold = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.IngredientID);
                });

            migrationBuilder.CreateTable(
                name: "ProductRecipes",
                columns: table => new
                {
                    RecipeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    IngredientID = table.Column<int>(type: "int", nullable: false),
                    QuantityRequired = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductRecipes", x => x.RecipeID);
                    table.ForeignKey(
                        name: "FK_ProductRecipes_Ingredients_IngredientID",
                        column: x => x.IngredientID,
                        principalTable: "Ingredients",
                        principalColumn: "IngredientID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductRecipes_Products_ProductID",
                        column: x => x.ProductID,
                        principalTable: "Products",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Cascade);
                });







            migrationBuilder.InsertData(
                table: "Expenses",
                columns: new[] { "ExpenseID", "Amount", "Category", "Description", "ExpenseDate", "ReferenceNumber", "Title" },
                values: new object[,]
                {
                    { 1, 150.00m, "Utilities", "Monthly power consumption", new DateTime(2026, 1, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Electricity Bill - Jan" },
                    { 2, 85.50m, "Supplies", "50L Fresh Milk", new DateTime(2026, 2, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Milk Supply Restock" },
                    { 3, 320.00m, "Supplies", "20kg Arabica beans", new DateTime(2026, 2, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Coffee Beans Cargo" }
                });

            migrationBuilder.InsertData(
                table: "Ingredients",
                columns: new[] { "IngredientID", "LowStockThreshold", "Name", "StockQuantity", "Unit" },
                values: new object[,]
                {
                    { 1, 2m, "Espresso Beans", 10m, "kg" },
                    { 2, 5m, "Fresh Milk", 20m, "L" },
                    { 3, 1000m, "Caramel Syrup", 5000m, "ml" },
                    { 4, 1000m, "Fructose", 5000m, "ml" },
                    { 5, 10m, "Pastry Flour", 50m, "kg" }
                });

            migrationBuilder.InsertData(
                table: "Promotions",
                columns: new[] { "PromotionID", "DiscountType", "DiscountValue", "EndDate", "IsActive", "PromotionName", "StartDate" },
                values: new object[,]
                {
                    { 1, "Percentage", 10m, new DateTime(2026, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "Early Bird Discount", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 2, "Fixed", 5m, new DateTime(2026, 2, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), true, "Grand Opening Special", new DateTime(2026, 2, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });







            migrationBuilder.InsertData(
                table: "ProductRecipes",
                columns: new[] { "RecipeID", "IngredientID", "ProductID", "QuantityRequired" },
                values: new object[,]
                {
                    { 1, 1, 1, 0.018m },
                    { 2, 1, 2, 0.018m },
                    { 3, 2, 2, 0.250m },
                    { 4, 3, 2, 30m },
                    { 5, 4, 2, 10m }
                });


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "ProductRecipes");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "Ingredients");
        }
    }
}
