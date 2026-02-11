using ljp_itsolutions.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ljp_itsolutions.ViewComponents
{
    public class NotificationsViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public NotificationsViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var notifications = new List<NotificationItem>();

            // Check Low Stock Products Logic Removed as per requirement
            /* 
             * Products are now treated as menu items with ingredient-based inventory.
             * Stock tracking for individual products is disabled.
             */

            // Check Low Stock Ingredients
            var lowStockIngredients = await _context.Ingredients
                .Where(i => i.StockQuantity <= i.LowStockThreshold)
                .OrderBy(i => i.StockQuantity)
                .Take(3)
                .ToListAsync();

            foreach (var i in lowStockIngredients)
            {
                notifications.Add(new NotificationItem
                {
                    Title = "Low Ingredient Stock",
                    Message = $"{i.Name} needs restocking ({i.StockQuantity:0.##} {i.Unit}).",
                    Time = "Just now",
                    Type = "danger",
                    IconClass = "fas fa-cube"
                });
            }

            // Check Recent Large Orders (e.g., > 500)
            var recentLargeOrder = await _context.Orders
                .Where(o => o.TotalAmount > 500)
                .OrderByDescending(o => o.OrderDate)
                .FirstOrDefaultAsync();

            if (recentLargeOrder != null && (DateTime.Now - recentLargeOrder.OrderDate).TotalHours < 24)
            {
                var timeAgo = GetTimeAgo(recentLargeOrder.OrderDate);
                notifications.Add(new NotificationItem
                {
                    Title = "High Value Order",
                    Message = $"Order #{recentLargeOrder.OrderID} for {recentLargeOrder.FinalAmount:C} received!",
                    Time = timeAgo,
                    Type = "success",
                    IconClass = "fas fa-star"
                });
            }

            return View(notifications);
        }

        private string GetTimeAgo(DateTime dateTime)
        {
            var span = DateTime.Now - dateTime;
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} mins ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
            return "Yesterday";
        }
    }

    public class NotificationItem
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string Time { get; set; } = "";
        public string Type { get; set; } = "info"; // success, warning, danger, info
        public string IconClass { get; set; } = "fas fa-bell";
    }
}
