using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using ljp_itsolutions.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = UserRoles.SuperAdmin)]
    public class SuperAdminController : Controller
    {
        private readonly InMemoryStore _store;
        private readonly ljp_itsolutions.Data.ApplicationDbContext _db;
        private readonly IPasswordHasher<ljp_itsolutions.Models.User> _hasher;

        public SuperAdminController(InMemoryStore store, ljp_itsolutions.Data.ApplicationDbContext db, IPasswordHasher<ljp_itsolutions.Models.User> hasher)
        {
            _store = store;
            _db = db;
            _hasher = hasher;
        }

        private async Task LogAudit(string action, Guid? userId = null)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    Action = action,
                    Timestamp = DateTime.Now,
                    UserID = userId ?? (User.Identity?.IsAuthenticated == true ? GetCurrentUserId() : null)
                };
                _db.AuditLogs.Add(auditLog);
                await _db.SaveChangesAsync();
            }
            catch { }
        }

        private Guid? GetCurrentUserId()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return null;
            var user = _db.Users.FirstOrDefault(u => u.Username == username);
            return user?.UserID;
        }

        public IActionResult Dashboard()
        {
            var auditLogs = _db.AuditLogs.Include(a => a.User).OrderByDescending(a => a.Timestamp).Take(10).ToList();
            var userCount = _db.Users.Count();
            var activeUsers = _db.Users.Count(u => u.IsActive);
            
            // Security metrics
            var failedLoginsCount = _db.AuditLogs.Count(a => a.Action.Contains("Failed login"));
            var lockedOutUsersCount = _db.Users.Count(u => u.LockoutEnd != null && u.LockoutEnd > DateTime.Now);

            var model = new {
                AuditLogs = auditLogs,
                UserCount = userCount,
                ActiveUsers = activeUsers,
                FailedLoginsCount = failedLoginsCount,
                LockedOutUsersCount = lockedOutUsersCount,
                SystemUptime = "99.9%",
                GrowthIndex = "+12.5%"
            };
            return View(model);
        }

        // --- User Management ---
        public IActionResult Users(bool showArchived = false)
        {
            var query = _db.Users.AsQueryable();
            if (showArchived) query = query.Where(u => !u.IsActive);
            else query = query.Where(u => u.IsActive);

            var users = query.OrderByDescending(u => u.CreatedAt).ToList();
            ViewBag.Roles = new List<string> { UserRoles.SuperAdmin, UserRoles.Admin, UserRoles.Manager, UserRoles.Cashier, UserRoles.MarketingStaff };
            ViewBag.ShowArchived = showArchived;
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(User user, string Password)
        {
            if (ModelState.IsValid)
            {
                if (_db.Users.Any(u => u.Username == user.Username))
                {
                    TempData["Error"] = "Username already exists.";
                    return RedirectToAction("Users");
                }
                user.UserID = Guid.NewGuid();
                user.CreatedAt = DateTime.Now;
                user.IsActive = true;
                user.Password = _hasher.HashPassword(user, Password);
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
                await LogAudit($"Created user: {user.Username}", user.UserID);
                TempData["Success"] = "User created successfully.";
            }
            return RedirectToAction("Users");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(User updatedUser)
        {
            var user = await _db.Users.FindAsync(updatedUser.UserID);
            if (user == null) return NotFound();
            user.FullName = updatedUser.FullName;
            user.Username = updatedUser.Username;
            user.Email = updatedUser.Email;
            user.Role = updatedUser.Role;
            user.IsActive = updatedUser.IsActive;
            await _db.SaveChangesAsync();
            await LogAudit($"Updated user: {user.Username}", user.UserID);
            TempData["Success"] = "User updated successfully.";
            return RedirectToAction("Users");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveUser(string id)
        {
            if (!Guid.TryParse(id, out var guid)) return RedirectToAction("Users");
            var user = await _db.Users.FindAsync(guid);
            if (user != null)
            {
                user.IsActive = false;
                await _db.SaveChangesAsync();
                await LogAudit($"Archived user: {user.Username}", user.UserID);
                TempData["Success"] = "User archived successfully.";
            }
            return RedirectToAction("Users");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreUser(string id)
        {
            if (!Guid.TryParse(id, out var guid)) return RedirectToAction("Users");
            var user = await _db.Users.FindAsync(guid);
            if (user != null)
            {
                user.IsActive = true;
                await _db.SaveChangesAsync();
                await LogAudit($"Restored user: {user.Username}", user.UserID);
                TempData["Success"] = "User restored successfully.";
            }
            return RedirectToAction("Users", new { showArchived = true });
        }

        // --- Audit Logs ---
        public IActionResult AuditLogs()
        {
            var logs = _db.AuditLogs.Include(a => a.User).OrderByDescending(a => a.Timestamp).ToList();
            ViewBag.Users = _db.Users.OrderBy(u => u.FullName).Select(u => new { u.UserID, u.FullName }).ToList();
            return View(logs);
        }

        // --- System Settings ---
        public IActionResult SystemSettings()
        {
            var settings = _db.SystemSettings.ToDictionary(s => s.SettingKey, s => s.SettingValue);
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSettings(IFormCollection form)
        {
            try {
                var keys = new[] { 
                    "SystemName", "Timezone", "Currency", "DateFormat", 
                    "SessionTimeout", "PasswordMinLength", "RequireSpecialChars", "RequireNumbers", "TwoFactorAuth",
                    "CompanyName", "TaxRate", "LowStockThreshold", "BusinessHourStart", "BusinessHourEnd",
                    "SmtpServer", "SmtpPort", "EmailNotifications", "LowStockAlerts", "DailyReports"
                };
                foreach (var key in keys) {
                    string value = form.ContainsKey(key) ? form[key].ToString() : "";
                    bool isCheckbox = key.Contains("Require") || key.Contains("TwoFactor") || key.Contains("Notifications") || key.Contains("Alerts") || key.Contains("DailyReports");
                    if (isCheckbox)
                        value = (!string.IsNullOrEmpty(value) && (value.ToLower() == "true" || value.ToLower() == "on")) ? "true" : "false";

                    var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
                    if (setting == null) {
                        setting = new SystemSetting { SettingKey = key, SettingValue = value };
                        _db.SystemSettings.Add(setting);
                    } else setting.SettingValue = value;
                }
                await _db.SaveChangesAsync();
                await LogAudit("Updated system settings");
                TempData["Success"] = "Settings updated.";
            } catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction("SystemSettings");
        }

        // --- Backups ---
        public IActionResult Backups()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            var files = Directory.GetFiles(path, "*.json").Select(f => new FileInfo(f)).OrderByDescending(f => f.CreationTime).ToList();
            return View(files);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBackup()
        {
            try {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                var fileName = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var fullPath = Path.Combine(path, fileName);
                var backupData = new {
                    GeneratedAt = DateTime.Now,
                    Users = await _db.Users.AsNoTracking().ToListAsync(),
                    Products = await _db.Products.AsNoTracking().ToListAsync(),
                    Orders = await _db.Orders.AsNoTracking().Include(o => o.OrderDetails).ToListAsync(),
                    Settings = await _db.SystemSettings.AsNoTracking().ToListAsync()
                };
                var json = JsonSerializer.Serialize(backupData, new JsonSerializerOptions { WriteIndented = true, ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles });
                await System.IO.File.WriteAllTextAsync(fullPath, json);
                await LogAudit("Created system backup");
                TempData["Success"] = "Backup created.";
            } catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction("Backups");
        }
    }
}
