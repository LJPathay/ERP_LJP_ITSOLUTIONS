using System.Collections.Generic;
using ljp_itsolutions.Models;

namespace ljp_itsolutions.Models
{
    public class ManagerDashboardViewModel
    {
        public int TotalProducts { get; set; }
        public int TotalUsers { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<Ingredient> LowStockIngredients { get; set; }
        public List<Order> RecentOrders { get; set; }
        public List<ProductSalesSummary> TopProducts { get; set; }
    }

    public class ProductSalesSummary
    {
        public string ProductName { get; set; }
        public int TotalSold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class InventoryViewModel
    {
        public List<Product> Products { get; set; } = new();
        public List<Ingredient> Ingredients { get; set; } = new();
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public int HealthyStockCount { get; set; }
    }

    public class FinanceViewModel
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit => TotalRevenue - TotalExpenses;
        public List<Expense> Expenses { get; set; } = new();
        public List<Order> RecentTransactions { get; set; } = new();
    }
}
