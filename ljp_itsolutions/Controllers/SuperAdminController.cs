using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using ljp_itsolutions.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Security.Claims;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = UserRoles.SuperAdmin)]
    public class SuperAdminController : BaseController
    {
        private readonly InMemoryStore _store;
        private readonly IPasswordHasher<ljp_itsolutions.Models.User> _hasher;
        private readonly IReceiptService _receiptService;
        private readonly IServiceScopeFactory _scopeFactory;

        public SuperAdminController(InMemoryStore store, ljp_itsolutions.Data.ApplicationDbContext db, IPasswordHasher<ljp_itsolutions.Models.User> hasher, IReceiptService receiptService, IServiceScopeFactory scopeFactory)
            : base(db)
        {
            _store = store;
            _hasher = hasher;
            _receiptService = receiptService;
            _scopeFactory = scopeFactory;
        }



        public IActionResult Dashboard()
        {
            var auditLogs = _db.AuditLogs.Include(a => a.User).OrderByDescending(a => a.Timestamp).Take(10).ToList();
            var userCount = _db.Users.Count();
            var activeUsers = _db.Users.Count(u => u.IsActive);
            
            // Security metrics
            var failedLoginsCount = _db.AuditLogs.Count(a => a.Action.Contains("Failed login"));
            var lockedOutUsersCount = _db.Users.Count(u => u.LockoutEnd != null && u.LockoutEnd > DateTime.UtcNow);

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
            
            // Excluded Superadmin role in adding a user in superadmin as an superadmin can add another superadmin???
            ViewBag.Roles = new List<string> { 
                UserRoles.Admin, 
                UserRoles.Manager, 
                UserRoles.Cashier, 
                UserRoles.MarketingStaff 
            };
            
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

                // SECURITY: Prevent creating another SuperAdmin
                if (user.Role == UserRoles.SuperAdmin)
                {
                    TempData["Error"] = "Unauthorized role assignment.";
                    return RedirectToAction("Users");
                }

                user.UserID = Guid.NewGuid();
                user.CreatedAt = DateTime.UtcNow;
                user.IsActive = true;
                
                // Secure Invite Link flow
                var token = Guid.NewGuid().ToString("N");
                user.PasswordResetToken = token;
                user.ResetTokenExpiry = DateTime.UtcNow.AddHours(2);
                user.Password = "LOCKED_INITIALLY"; 

                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                // Send Invite Email
                if (!string.IsNullOrEmpty(user.Email))
                {
                    var inviteLink = Url.Action("ResetPassword", "Account", new { userId = user.UserID, token = token }, protocol: Request.Scheme) ?? "";
                    // Send welcome email invite in background
                    _ = Task.Run(async () => {
                        try {
                            using (var scope = _scopeFactory.CreateScope()) {
                                var scopedReceiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                                await scopedReceiptService.SendStaffInviteAsync(user, inviteLink);
                            }
                        } catch (Exception ex) {
                            // Log locally but don't block
                            Console.WriteLine($"[Email Failure]: {ex.Message}");
                        }
                    });
                }

                await LogAudit($"Created user: {user.Username} as {user.Role}", $"Target User ID: {user.UserID}");
                TempData["Success"] = "User created successfully. Secure invite link sent to their email.";
            }
            return RedirectToAction("Users");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(User updatedUser)
        {
            var user = await _db.Users.FindAsync(updatedUser.UserID);
            if (user == null) return NotFound();

            if (user.Role == UserRoles.SuperAdmin || updatedUser.Role == UserRoles.SuperAdmin)
            {
                TempData["Error"] = "Restricted access: SuperAdmin accounts cannot be modified here.";
                return RedirectToAction("Users");
            }

            user.FullName = updatedUser.FullName;
            user.Username = updatedUser.Username;
            user.Email = updatedUser.Email;
            user.Role = updatedUser.Role;
            user.IsActive = updatedUser.IsActive;
            await _db.SaveChangesAsync();
            await LogAudit($"Updated user: {user.Username}", $"Target User ID: {user.UserID}");
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
                if (user.Role == UserRoles.SuperAdmin)
                {
                    TempData["Error"] = "SuperAdmin accounts cannot be archived.";
                    return RedirectToAction("Users");
                }

                var archivedUser = new ArchivedUser
                {
                    OriginalUserID = user.UserID,
                    Username = user.Username,
                    FullName = user.FullName,
                    Email = user.Email ?? "",
                    Role = user.Role,
                    ArchivedAt = DateTime.UtcNow,
                    Reason = "User requested archive"
                };

                _db.ArchivedUsers.Add(archivedUser);
                _db.Users.Remove(user);
                
                await _db.SaveChangesAsync();
                await LogAudit($"Archived user: {user.Username}", $"Target User ID: {user.UserID}");
                TempData["Success"] = "User moved to archives successfully.";
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
                await LogAudit($"Restored user: {user.Username}", $"Target User ID: {user.UserID}");
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
                    "SmtpServer", "SmtpPort", "EmailNotifications", "LowStockAlerts", "DailyReports",
                    "MaintenanceMode", "LogRetentionDays", "AllowPublicRegistration"
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
                var fileName = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                var fullPath = Path.Combine(path, fileName);
                var backupData = new {
                    GeneratedAt = DateTime.UtcNow,
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
