using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ljp_itsolutions.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipeTemplates",
                columns: table => new
                {
                    RecipeTemplateID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeTemplates", x => x.RecipeTemplateID);
                });

            migrationBuilder.CreateTable(
                name: "RecipeTemplateIngredients",
                columns: table => new
                {
                    RecipeTemplateIngredientID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeTemplateID = table.Column<int>(type: "int", nullable: false),
                    IngredientName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeTemplateIngredients", x => x.RecipeTemplateIngredientID);
                    table.ForeignKey(
                        name: "FK_RecipeTemplateIngredients_RecipeTemplates_RecipeTemplateID",
                        column: x => x.RecipeTemplateID,
                        principalTable: "RecipeTemplates",
                        principalColumn: "RecipeTemplateID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 21, 11, 19, 33, 190, DateTimeKind.Local).AddTicks(7422));

            migrationBuilder.CreateIndex(
                name: "IX_RecipeTemplateIngredients_RecipeTemplateID",
                table: "RecipeTemplateIngredients",
                column: "RecipeTemplateID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeTemplateIngredients");

            migrationBuilder.DropTable(
                name: "RecipeTemplates");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: new Guid("4f7b6d1a-5b6c-4d8e-a9f2-0a1b2c3d4e5f"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 20, 18, 7, 1, 934, DateTimeKind.Local).AddTicks(5810));
        }
    }
}
