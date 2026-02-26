using ljp_itsolutions.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System;
using ljp_itsolutions.Models;

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
                // Get current user context
                var user = HttpContext.User;
                var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
                Guid? currentUserId = null;
                if (Guid.TryParse(userIdStr, out var uid)) currentUserId = uid;

                var userRole = HttpContext.Session.GetString("UserRole") ?? user.FindFirstValue(ClaimTypes.Role) ?? "-";

                // Fetch latest notifications filtered for the current user
                var dbNotifications = await _context.Notifications
                    .Where(n => !n.IsRead && (
                        n.UserID == currentUserId || 
                        (n.UserID == null && (
                            (userRole == "Manager" && (n.Title.Contains("Needed") || n.Title.Contains("Stock") || n.Title.Contains("Order")))
                            ||
                            (userRole == "MarketingStaff" && (n.Title.Contains("Approved") || n.Title.Contains("Rejected")))
                            ||
                            ((userRole == "Admin" || userRole == "SuperAdmin") && 
                             (n.Title.Contains("Needed") || n.Title.Contains("Stock") || n.Title.Contains("Order") || n.Title.Contains("Approved") || n.Title.Contains("Rejected")))
                        ))
                    ))
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(10)
                    .ToListAsync();

                ViewBag.UnreadCount = await _context.Notifications.CountAsync(n => !n.IsRead && (
                        n.UserID == currentUserId || 
                        (n.UserID == null && (
                            (userRole == "Manager" && (n.Title.Contains("Needed") || n.Title.Contains("Stock") || n.Title.Contains("Order")))
                            ||
                            (userRole == "MarketingStaff" && (n.Title.Contains("Approved") || n.Title.Contains("Rejected")))
                            ||
                            ((userRole == "Admin" || userRole == "SuperAdmin") && 
                             (n.Title.Contains("Needed") || n.Title.Contains("Stock") || n.Title.Contains("Order") || n.Title.Contains("Approved") || n.Title.Contains("Rejected")))
                        ))
                    ));

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
                // Fail silently
            }

            return View(notifications);
        }

        private string GetTimeAgo(DateTime dateTime)
        {
            var span = DateTime.UtcNow - dateTime; 
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
