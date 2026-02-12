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

            var model = new
            {
                TotalCustomers = totalCustomers,
                ActivePromotions = activePromotions,
                TotalOrders = totalOrders,
                TotalPointsAwarded = totalPointsAwarded
            };

            return View(model);
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
            var topCustomers = await _db.Customers
                .OrderByDescending(c => c.Points)
                .Take(10)
                .ToListAsync();
            return View(topCustomers);
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
