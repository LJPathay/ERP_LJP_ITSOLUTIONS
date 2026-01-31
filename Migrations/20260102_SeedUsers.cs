using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ljp_itsolutions.Migrations
{
    public partial class SeedUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Username", "Email", "Password", "FullName", "Role", "IsArchived" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "admin", "admin@coffee.local", "AQAAAAEAACcQAAAAEAdminPlaceholderHash", "System Admin", 0, false },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "manager", "manager@coffee.local", "AQAAAAEAACcQAAAAEManagerPlaceholderHash", "Store Manager", 1, false },
                    { new Guid("33333333-3333-3333-3333-333333333333"), "cashier", "cashier@coffee.local", "AQAAAAEAACcQAAAAECashierPlaceholderHash", "Cashier", 2, false },
                    { new Guid("44444444-4444-4444-4444-444444444444"), "marketing", "marketing@coffee.local", "AQAAAAEAACcQAAAAEMarketingPlaceholderHash", "Marketing", 3, false }
                }
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "Users", keyColumn: "Id", keyValue: new Guid("11111111-1111-1111-1111-111111111111"));
            migrationBuilder.DeleteData(table: "Users", keyColumn: "Id", keyValue: new Guid("22222222-2222-2222-2222-222222222222"));
            migrationBuilder.DeleteData(table: "Users", keyColumn: "Id", keyValue: new Guid("33333333-3333-3333-3333-333333333333"));
            migrationBuilder.DeleteData(table: "Users", keyColumn: "Id", keyValue: new Guid("44444444-4444-4444-4444-444444444444"));
        }
    }
}
