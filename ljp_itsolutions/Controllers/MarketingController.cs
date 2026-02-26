using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Data;
using ljp_itsolutions.Models;
using ljp_itsolutions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "MarketingStaff,Admin,SuperAdmin")]
    public class MarketingController : BaseController
    {
        private readonly IReceiptService _receiptService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAnalyticsService _analyticsService;

        public MarketingController(ApplicationDbContext db, IReceiptService receiptService, IServiceScopeFactory scopeFactory, IAnalyticsService analyticsService)
            : base(db)
        {
            _receiptService = receiptService;
            _scopeFactory = scopeFactory;
            _analyticsService = analyticsService;
        }

        //  Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var data = await _analyticsService.GetMarketingDashboardDataAsync();
            return View(data);
        }


        public class RewardRequest
        {
            public int CustomerId { get; set; }
            public int PointsToDeduct { get; set; }
            public decimal DiscountValue { get; set; }
            public string RewardName { get; set; } = string.Empty;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateReward([FromBody] RewardRequest req)
        {
            var customer = await _db.Customers.FindAsync(req.CustomerId);
            if (customer == null) return NotFound();

            if (customer.Points < req.PointsToDeduct)
                return Json(new { success = false, message = $"Customer needs at least {req.PointsToDeduct} points for a reward." });

            // Build a promo code
            var cleanName = customer.FullName.Replace(" ", "").ToUpper();
            if (cleanName.Length > 5) cleanName = cleanName.Substring(0, 5);
            var promoCode = $"REWARD-{cleanName}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";

            // Create the promotion entry
            var promotion = new Promotion
            {
                PromotionName = promoCode,
                DiscountType  = "Percentage",
                DiscountValue = req.DiscountValue,
                StartDate     = DateTime.UtcNow,
                EndDate       = DateTime.UtcNow.AddDays(30),
                IsActive      = true,
                ApprovalStatus = "Approved",
                ApprovedDate  = DateTime.UtcNow,
                MaxRedemptions = 1,
                OneTimePerCustomer = true,
                IsOneTimeReward = true,
                TargetAudience = "Specific VIP"
            };
            _db.Promotions.Add(promotion);

            // Deduct points from customer
            customer.Points -= req.PointsToDeduct;

            // Log the redemption event
            _db.RewardRedemptions.Add(new RewardRedemption
            {
                CustomerID      = customer.CustomerID,
                RewardName      = req.RewardName,
                PointsRedeemed  = req.PointsToDeduct,
                RedemptionDate  = DateTime.Now
            });

            await _db.SaveChangesAsync();
            await LogAudit("Generated VIP Loyalty Reward", $"Customer: {customer.FullName}, Code: {promoCode} for {req.RewardName}");

            // Send reward code to customer email in background
            if (!string.IsNullOrEmpty(customer.Email))
            {
                var capturedCustomer = customer;
                var capturedCode     = promoCode;
                var capturedDiscount = req.DiscountValue;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var svc = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                        await svc.SendRedemptionCodeEmailAsync(capturedCustomer, capturedCode, capturedDiscount);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Email Failure - Reward Code]: {ex.Message}");
                    }
                });
            }

            bool emailSent = !string.IsNullOrEmpty(customer.Email);
            string emailNote = emailSent
                ? $" A copy has been sent to {customer.Email} with a scannable QR code."
                : " (No email on file — show code manually.)";

            return Json(new
            {
                success   = true,
                promoCode = promoCode,
                message   = $"Reward provsioned successfully for {customer.FullName}!{emailNote}"
            });
        }

        //  Promotions
        public async Task<IActionResult> Promotions()
        {
            var promotions = await _db.Promotions
                .Include(p => p.Orders)
                .ToListAsync();
            return View(promotions);
        }

        public IActionResult CreateCampaign()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCampaign(Promotion promotion)
        {
            if (ModelState.IsValid)
            {
                promotion.ApprovalStatus = "Pending";
                promotion.ApprovedDate = null;
                promotion.CreatedBy = GetCurrentUserId();
                
                _db.Promotions.Add(promotion);
                
                // Notify Management
                _db.Notifications.Add(new Notification
                {
                    Title = "Promotion Approval Needed",
                    Message = $"Campaign '{promotion.PromotionName}' was created by {User.Identity?.Name} and is pending approval.",
                    Type = "info",
                    IconClass = "fas fa-bullhorn",
                    CreatedAt = DateTime.UtcNow,
                    TargetUrl = "/Manager/Promotions"
                });

                await _db.SaveChangesAsync();
                await LogAudit($"Created Campaign: {promotion.PromotionName}");
                
                TempData["SuccessMessage"] = $"Campaign '{promotion.PromotionName}' created and submitted for manager approval.";
                return RedirectToAction(nameof(Promotions));
            }
            return View(promotion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCampaign(Promotion promotion)
        {
            var existing = await _db.Promotions.FindAsync(promotion.PromotionID);
            if (existing == null) return NotFound();

            existing.PromotionName = promotion.PromotionName;
            existing.DiscountType = promotion.DiscountType;
            existing.DiscountValue = promotion.DiscountValue;
            existing.StartDate = promotion.StartDate;
            existing.EndDate = promotion.EndDate;
            existing.IsActive = promotion.IsActive;
            existing.MaxRedemptions = promotion.MaxRedemptions;
            existing.OneTimePerCustomer = promotion.OneTimePerCustomer;
            
          
            existing.ApprovalStatus = "Pending";
            existing.ApprovedBy = null;
            existing.ApprovedDate = null;
            existing.CreatedBy = GetCurrentUserId();

            // Notify Management
            _db.Notifications.Add(new Notification
            {
                Title = "Promotion Approval Needed",
                Message = $"Campaign '{existing.PromotionName}' was edited and requires re-approval.",
                Type = "warning",
                IconClass = "fas fa-edit",
                CreatedAt = DateTime.UtcNow,
                TargetUrl = "/Manager/Promotions"
            });

            await _db.SaveChangesAsync();
            await LogAudit($"Edited Campaign: {promotion.PromotionName}");
            TempData["SuccessMessage"] = "Campaign updated and resubmitted for approval.";
            return RedirectToAction(nameof(Promotions));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCampaign(int id)
        {
            var promotion = await _db.Promotions.FindAsync(id);
            if (promotion != null)
            {
                var promoName = promotion.PromotionName;
                _db.Promotions.Remove(promotion);
                await _db.SaveChangesAsync();
                await LogAudit($"Deleted Campaign: {promoName}");
                TempData["SuccessMessage"] = "Campaign deleted successfully.";
            }
            return RedirectToAction(nameof(Promotions));
        }

        public async Task<IActionResult> PromotionPerformance()
        {
            var performance = await _db.Promotions
                .Include(p => p.Orders)
                .Select(p => new
                {
                    p.PromotionID,
                    p.PromotionName,
                    p.TargetAudience,
                    p.StartDate,
                    p.EndDate,
                    p.IsActive,
                    UsageCount = p.Orders.Count,
                    TotalSalesValue = p.Orders.Sum(o => o.FinalAmount),
                    TotalDiscountGiven = p.Orders.Sum(o => o.DiscountAmount)
                })
                .ToListAsync();
            return View(performance);
        }

        public async Task<IActionResult> CampaignAnalytics(int id)
        {
            var campaign = await _db.Promotions
                .Include(p => p.Orders)
                    .ThenInclude(o => o.Customer)
                .FirstOrDefaultAsync(p => p.PromotionID == id);

            if (campaign == null) return NotFound();

            return View(campaign);
        }

        //  Customer Engagement
        public async Task<IActionResult> Customers()
        {
            var customers = await _db.Customers.ToListAsync();
            return View(customers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustomer(Customer customer)
        {
            if (ModelState.IsValid)
            {
                customer.Points = 0;
                _db.Customers.Add(customer);
                await _db.SaveChangesAsync();
                await LogAudit($"Enrolled Customer: {customer.FullName}");
                TempData["SuccessMessage"] = "Customer added successfully.";
            }
            return RedirectToAction(nameof(Customers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustomer(Customer customer)
        {
            var existing = await _db.Customers.FindAsync(customer.CustomerID);
            if (existing == null) return NotFound();

            existing.FullName = customer.FullName;
            existing.PhoneNumber = customer.PhoneNumber;
            existing.Email = customer.Email;
            existing.Points = customer.Points;

            await _db.SaveChangesAsync();
            await LogAudit($"Edited Customer: {customer.FullName}");
            TempData["SuccessMessage"] = "Customer updated successfully.";
            return RedirectToAction(nameof(Customers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _db.Customers.FindAsync(id);
            if (customer != null)
            {
                var custName = customer.FullName;
                _db.Customers.Remove(customer);
                await _db.SaveChangesAsync();
                await LogAudit($"Deleted Customer: {custName}");
                TempData["SuccessMessage"] = "Customer removed successfully.";
            }
            return RedirectToAction(nameof(Customers));
        }

        public async Task<IActionResult> LoyaltyOverview()
        {
            var customers = await _db.Customers.ToListAsync();
            
            // Data for Tier Distribution Chart
            var tiers = new
            {
                Gold = customers.Count(c => c.Points > 1000),
                Silver = customers.Count(c => c.Points > 500 && c.Points <= 1000),
                Bronze = customers.Count(c => c.Points <= 500)
            };

            var topHolders = customers.OrderByDescending(c => c.Points).Take(10).ToList();

            ViewBag.TierLabels = new[] { "Gold (>1000)", "Silver (501-1000)", "Bronze (0-500)" };
            ViewBag.TierData = new[] { tiers.Gold, tiers.Silver, tiers.Bronze };

            return View(topHolders);
        }

        public async Task<IActionResult> RewardRedemptionLogs()
        {
            var logs = await _db.RewardRedemptions
                .Include(r => r.Customer)
                .OrderByDescending(r => r.RedemptionDate)
                .ToListAsync();
            return View(logs);
        }

        //  Reports
        public async Task<IActionResult> SalesTrends(string month)
        {
            DateTime selectedMonth = DateTime.Today;
            if (!string.IsNullOrEmpty(month) && DateTime.TryParse(month + "-01", out var parsedMonth))
            {
                selectedMonth = parsedMonth;
            }

            var startOfMonth = new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            var salesData = await _db.Orders
                .Where(o => o.OrderDate >= startOfMonth && o.OrderDate <= endOfMonth && 
                           (o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)" || o.PaymentStatus == "Completed"))
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new { Date = g.Key, TotalSales = g.Sum(o => o.FinalAmount) })
                .OrderBy(g => g.Date)
                .ToListAsync();

            ViewBag.SelectedMonth = selectedMonth.ToString("yyyy-MM");
            return View(salesData);
        }

        [HttpGet]
        public async Task<IActionResult> ExportTacticalROI()
        {
            byte[] buffer = await _analyticsService.GenerateTacticalROICSVAsync();
            return File(buffer, "text/csv", $"LJP_Marketing_ROI_{DateTime.Now:yyyyMMdd}.csv");
        }

        [HttpGet]
        public async Task<IActionResult> ExportSalesTrends(string month)
        {
            DateTime selectedMonth = DateTime.Today;
            if (!string.IsNullOrEmpty(month) && DateTime.TryParse(month + "-01", out var parsedMonth))
            {
                selectedMonth = parsedMonth;
            }

            byte[] buffer = await _analyticsService.GenerateSalesTrendsCSVAsync(selectedMonth);
            return File(buffer, "text/csv", $"LJP_Sales_Trend_{selectedMonth:yyyyMM}.csv");
        }
    }
}
