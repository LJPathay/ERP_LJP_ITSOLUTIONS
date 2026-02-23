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

            try
            {
                // Fetch latest notifications from database
                var dbNotifications = await _context.Notifications
                    .Where(n => !n.IsRead) // focus on unread
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(10)
                    .ToListAsync();

                ViewBag.UnreadCount = await _context.Notifications.CountAsync(n => !n.IsRead);

                foreach (var n in dbNotifications)
                {
                    notifications.Add(new NotificationItem
                    {
                        Id = n.NotificationID,
                        Title = n.Title,
                        Message = n.Message,
                        Time = n.CreatedAt == DateTime.MinValue ? "Just now" : GetTimeAgo(n.CreatedAt),
                        Type = n.Type,
                        IconClass = n.IconClass,
                        IsRead = n.IsRead,
                        TargetUrl = string.IsNullOrEmpty(n.TargetUrl) 
                            ? (n.Title.Contains("Stock") ? "/Manager/Inventory" : (n.Title.Contains("Order") ? "/Manager/Transactions" : "#"))
                            : n.TargetUrl
                    });
                }
            }
            catch
            {
                // Fail silently or log to a real logger if available
            }

            return View(notifications);
        }

        private string GetTimeAgo(DateTime dateTime)
        {
            var span = DateTime.Now - dateTime;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            return dateTime.ToString("MMM dd");
        }
    }

    public class NotificationItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Type { get; set; } = "info";
        public string IconClass { get; set; } = "fas fa-bell";
        public bool IsRead { get; set; }
        public string? TargetUrl { get; set; }
    }
}
