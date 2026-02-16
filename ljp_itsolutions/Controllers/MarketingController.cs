using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Data;
using ljp_itsolutions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "MarketingStaff,Admin")]
    public class MarketingController : Controller
    {
        private readonly ApplicationDbContext _db;

        public MarketingController(ApplicationDbContext db)
        {
            _db = db;
        }

        // 📊 Dashboard (Analytics Overview)
        public async Task<IActionResult> Dashboard()
        {
            var totalCustomers = await _db.Customers.CountAsync();
            var activePromotions = await _db.Promotions.CountAsync(p => p.IsActive && p.EndDate >= DateTime.Now);
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

            var model = new
            {
                TotalCustomers = totalCustomers,
                ActivePromotions = activePromotions,
                TotalOrders = totalOrders,
                TotalPointsAwarded = totalPointsAwarded,
                TopCustomers = topCustomers,
                ChartLabels = chartLabels,
                ChartData = chartData
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> GenerateReward(int customerId)
        {
            var customer = await _db.Customers.FindAsync(customerId);
            if (customer == null) return NotFound();

            if (customer.Points < 10) 
                return Json(new { success = false, message = "Customer needs at least 10 points for a reward." });

            // Create a unique promo code
            var cleanName = customer.FullName.Replace(" ", "").ToUpper();
            if (cleanName.Length > 5) cleanName = cleanName.Substring(0, 5);
            var promoCode = $"REWARD-{cleanName}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";

            var promotion = new Promotion
            {
                PromotionName = $"Loyalty Reward - {customer.FullName}",
                DiscountType = "Percentage",
                DiscountValue = 15, // 15% off reward
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddDays(30),
                IsActive = true
            };

            _db.Promotions.Add(promotion);
            
            // Deduct points (optional logic - let's deduct 10 points per reward)
            customer.Points -= 10;
            
            await _db.SaveChangesAsync();

            return Json(new { success = true, promoCode = promoCode, message = $"15% Discount code generated for {customer.FullName}!" });
        }

        // 🎯 Promotions
        public async Task<IActionResult> Promotions()
        {
            var promotions = await _db.Promotions.ToListAsync();
            return View(promotions);
        }

        public IActionResult CreateCampaign()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateCampaign(Promotion promotion)
        {
            if (ModelState.IsValid)
            {
                _db.Promotions.Add(promotion);
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Promotions));
            }
            return View(promotion);
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

        // 👥 Customer Engagement
        public async Task<IActionResult> Customers()
        {
            var customers = await _db.Customers.ToListAsync();
            return View(customers);
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

        // 📈 Reports
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
