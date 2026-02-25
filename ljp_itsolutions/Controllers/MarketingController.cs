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

        public MarketingController(ApplicationDbContext db, IReceiptService receiptService, IServiceScopeFactory scopeFactory)
            : base(db)
        {
            _receiptService = receiptService;
            _scopeFactory = scopeFactory;
        }

        //  Dashboard (Analytics Overview)
        public async Task<IActionResult> Dashboard()
        {
            var totalCustomers = await _db.Customers.CountAsync();
            var activePromotions = await _db.Promotions.CountAsync(p => p.IsActive && p.EndDate >= DateTime.UtcNow);
            var totalOrders = await _db.Orders.CountAsync();
            var totalPointsAwarded = await _db.Customers.SumAsync(c => (long)c.Points);

            var topCustomers = await _db.Customers
                .OrderByDescending(c => c.Points)
                .Take(5)
                .Select(c => new { c.CustomerID, c.FullName, c.Points })
                .ToListAsync();

            // Prepare Chart Data: Last 7 days of sales activity
            var last7Days = Enumerable.Range(0, 7)
                .Select(i => DateTime.Today.AddDays(-i))
                .OrderBy(d => d)
                .ToList();

            var salesActivity = await _db.Orders
                .Where(o => o.OrderDate >= last7Days.First())
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();

            var chartLabels = last7Days.Select(d => d.ToString("MMM dd")).ToList();
            var chartData = last7Days.Select(d => salesActivity.FirstOrDefault(s => s.Date == d)?.Count ?? 0).ToList();

            // Customer Retention: New vs Returning
            // Returning customers = customers who have placed at least one order
            var orderingCustomerIds = await _db.Orders.Select(o => o.CustomerID).Distinct().ToListAsync();
            var returningCustomersCount = orderingCustomerIds.Count;
            var newCustomersCount = totalCustomers - returningCustomersCount;

            var model = new
            {
                TotalCustomers = totalCustomers,
                ActivePromotions = activePromotions,
                TotalOrders = totalOrders,
                TotalPointsAwarded = totalPointsAwarded,
                TopCustomers = topCustomers,
                ChartLabels = chartLabels,
                ChartData = chartData,
                NewCustomersCount = newCustomersCount,
                ReturningCustomersCount = returningCustomersCount
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateReward(int customerId)
        {
            var customer = await _db.Customers.FindAsync(customerId);
            if (customer == null) return NotFound();

            if (customer.Points < 10)
                return Json(new { success = false, message = "Customer needs at least 10 points for a reward." });

            // Build a unique, readable promo code
            var cleanName = customer.FullName.Replace(" ", "").ToUpper();
            if (cleanName.Length > 5) cleanName = cleanName.Substring(0, 5);
            var promoCode = $"REWARD-{cleanName}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
            const decimal discountValue = 15m;

            // Create the promotion entry
            var promotion = new Promotion
            {
                PromotionName = promoCode,
                DiscountType  = "Percentage",
                DiscountValue = discountValue,
                StartDate     = DateTime.UtcNow,
                EndDate       = DateTime.UtcNow.AddDays(30),
                IsActive      = true,
                ApprovalStatus = "Approved",
                ApprovedDate  = DateTime.UtcNow,
                MaxRedemptions = 1,
                OneTimePerCustomer = true,
                IsOneTimeReward = true
            };
            _db.Promotions.Add(promotion);

            // Deduct 10 points from customer
            customer.Points -= 10;

            // Log the redemption event
            _db.RewardRedemptions.Add(new RewardRedemption
            {
                CustomerID      = customer.CustomerID,
                RewardName      = $"{discountValue}% Discount Code",
                PointsRedeemed  = 10,
                RedemptionDate  = DateTime.Now
            });

            await _db.SaveChangesAsync();
            await LogAudit("Generated Loyalty Reward", $"Customer: {customer.FullName}, Code: {promoCode}");

            // Send reward code to customer email in background
            if (!string.IsNullOrEmpty(customer.Email))
            {
                var capturedCustomer = customer;
                var capturedCode     = promoCode;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var svc = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                        await svc.SendRedemptionCodeEmailAsync(capturedCustomer, capturedCode, discountValue);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Email Failure - Reward Code]: {ex.Message}");
                    }
                });
            }

            bool emailSent = !string.IsNullOrEmpty(customer.Email);
            string emailNote = emailSent
                ? $" A copy has been sent to {customer.Email}."
                : " (No email on file — show code manually.)"
            ;
            return Json(new
            {
                success   = true,
                promoCode = promoCode,
                message   = $"{discountValue}% Discount code generated for {customer.FullName}!{emailNote}"
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
                promotion.ApprovedBy = null;
                promotion.ApprovedDate = null;
                
                _db.Promotions.Add(promotion);
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
                    p.PromotionName,
                    UsageCount = p.Orders.Count,
                    TotalDiscountedAmount = p.Orders.Sum(o => o.DiscountAmount)
                })
                .ToListAsync();
            return View(performance);
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
        public async Task<IActionResult> SalesTrends()
        {
            var salesData = await _db.Orders
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new { Date = g.Key, TotalSales = g.Sum(o => o.FinalAmount) })
                .OrderBy(g => g.Date)
                .ToListAsync();
            return View(salesData);
        }

        public async Task<IActionResult> CampaignReports()
        {
            var campaigns = await _db.Promotions
                .Include(p => p.Orders)
                .Select(p => new
                {
                    p.PromotionName,
                    p.StartDate,
                    p.EndDate,
                    p.IsActive,
                    UsageCount = p.Orders.Count,
                    TotalSalesValue = p.Orders.Sum(o => o.FinalAmount),
                    TotalDiscountGiven = p.Orders.Sum(o => o.DiscountAmount)
                })
                .ToListAsync();
            return View(campaigns);
        }

    }
}
