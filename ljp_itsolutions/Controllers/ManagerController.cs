using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Models;
using ljp_itsolutions.Services;
using Microsoft.AspNetCore.Authorization;
using ljp_itsolutions.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "Manager,Admin,SuperAdmin")]
    public class ManagerController : BaseController
    {
        private readonly IPhotoService _photoService;
        private readonly IRecipeService _recipeService;
        private readonly IReceiptService _receiptService;
        private readonly IServiceScopeFactory _scopeFactory;

        public ManagerController(ApplicationDbContext db, IPhotoService photoService, IRecipeService recipeService, IReceiptService receiptService, IServiceScopeFactory scopeFactory)
            : base(db)
        {
            _photoService = photoService;
            _recipeService = recipeService;
            _receiptService = receiptService;
            _scopeFactory = scopeFactory;
        }

        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> Dashboard()
        {
            var today = DateTime.Today;
            var sevenDaysAgo = today.AddDays(-6);
            var thirtyDaysAgo = today.AddDays(-29);

            // Chart Data: Daily Revenue (Last 7 Days)
            var recentOrders = await _db.Orders
                .Where(o => o.OrderDate >= sevenDaysAgo)
                .ToListAsync();

            var dailyRevenueLabels = new List<string>();
            var dailyRevenueData = new List<decimal>();

            for (int i = 0; i < 7; i++)
            {
                var date = sevenDaysAgo.AddDays(i);
                dailyRevenueLabels.Add(date.ToString("MMM dd"));
                dailyRevenueData.Add(recentOrders.Where(o => o.OrderDate.Date == date.Date && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)" || o.PaymentStatus == "Completed")).Sum(o => o.FinalAmount));
            }

            // Category Distribution
            var categoryData = await _db.OrderDetails
                .Include(od => od.Product)
                    .ThenInclude(p => p.Category)
                .GroupBy(od => od.Product.Category.CategoryName)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(5)
                .ToListAsync();

            // Growth Calculations (Last 30 days vs Previous 30 days)
            var sixtyDaysAgo = thirtyDaysAgo.AddDays(-30);
            
            var currentRevenue = await _db.Orders.Where(o => o.OrderDate >= thirtyDaysAgo && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)" || o.PaymentStatus == "Completed")).SumAsync(o => o.FinalAmount);
            var previousRevenue = await _db.Orders.Where(o => o.OrderDate >= sixtyDaysAgo && o.OrderDate < thirtyDaysAgo && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)" || o.PaymentStatus == "Completed")).SumAsync(o => o.FinalAmount);
            double revenueGrowth = previousRevenue > 0 ? (double)((currentRevenue - previousRevenue) / previousRevenue * 100) : 0;

            var currentOrdersCount = await _db.Orders.Where(o => o.OrderDate >= thirtyDaysAgo).CountAsync();
            var previousOrdersCount = await _db.Orders.Where(o => o.OrderDate >= sixtyDaysAgo && o.OrderDate < thirtyDaysAgo).CountAsync();
            double orderGrowth = previousOrdersCount > 0 ? (double)(currentOrdersCount - previousOrdersCount) / (double)previousOrdersCount * 100 : 0;

            var viewModel = new ManagerDashboardViewModel
            {
                TotalProducts = await _db.Products.CountAsync(),
                TotalUsers = await _db.Users.CountAsync(),
                TotalOrders = await _db.Orders.CountAsync(),
                TotalRevenue = await _db.Orders.Where(o => (o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)" || o.PaymentStatus == "Completed")).SumAsync(o => o.FinalAmount),
                
                RevenueGrowth = revenueGrowth,
                OrderGrowth = orderGrowth,
                
                DailyRevenueLabels = dailyRevenueLabels,
                DailyRevenueData = dailyRevenueData,
                CategoryLabels = categoryData.Select(c => c.Name).ToList(),
                CategoryData = categoryData.Select(c => c.Count).ToList(),

                LowStockIngredients = await _db.Ingredients
                    .Where(i => i.StockQuantity > 0 && i.StockQuantity < i.LowStockThreshold)
                    .OrderBy(i => i.StockQuantity)
                    .Take(5)
                    .ToListAsync(),
                RecentOrders = await _db.Orders
                    .Include(o => o.Cashier)
                    .OrderByDescending(o => o.OrderDate)
                    .Take(5)
                    .ToListAsync(),
                TopProducts = await _db.OrderDetails
                    .GroupBy(od => od.Product.ProductName)
                    .Select(g => new ProductSalesSummary
                    {
                        ProductName = g.Key,
                        TotalSold = g.Sum(od => od.Quantity),
                        Revenue = g.Sum(od => od.Subtotal)
                    })
                    .OrderByDescending(s => s.TotalSold)
                    .Take(5)
                    .ToListAsync()
            };

            // Hourly Sales Performance (Last 30 days)
            var hourlyData = await _db.Orders
                .Where(o => o.OrderDate >= thirtyDaysAgo)
                .GroupBy(o => o.OrderDate.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .ToListAsync();

            var peakHoursLabels = Enumerable.Range(0, 24).Select(h => DateTime.Today.AddHours(h).ToString("hh tt")).ToList();
            var peakHoursData = Enumerable.Range(0, 24).Select(h => hourlyData.FirstOrDefault(x => x.Hour == h)?.Count ?? 0).ToList();

            ViewBag.PeakHoursLabels = peakHoursLabels;
            ViewBag.PeakHoursData = peakHoursData;

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IntakeStock(int IngredientID, decimal Quantity, string Remarks, DateTime? IntakeDate, DateTime? ExpiryDate)
        {
            var ingredient = await _db.Ingredients.FindAsync(IngredientID);
            if (ingredient == null) return NotFound();

            if (Quantity <= 0)
            {
                TempData["ErrorMessage"] = "Intake quantity must be greater than zero.";
                return RedirectToAction("Inventory");
            }

            var actualDate = IntakeDate ?? DateTime.UtcNow;

            ingredient.StockQuantity += Quantity;
            ingredient.LastStockedDate = actualDate;
            
            if (ExpiryDate.HasValue)
            {
                ingredient.ExpiryDate = ExpiryDate.Value;
            }

            // Log to InventoryLogs
            string logRemarks = string.IsNullOrEmpty(Remarks) ? "Manual stock intake" : Remarks;
            if (ExpiryDate.HasValue)
            {
                logRemarks += $" (Expiry: {ExpiryDate.Value:yyyy-MM-dd})";
            }

            _db.InventoryLogs.Add(new InventoryLog
            {
                IngredientID = IngredientID,
                QuantityChange = Quantity,
                ChangeType = "Intake",
                LogDate = actualDate,
                Remarks = logRemarks
            });

            await _db.SaveChangesAsync();
            await LogAudit($"Stock Intake: Added {Quantity} {ingredient.Unit} to {ingredient.Name} on {actualDate:yyyy-MM-dd}");
            
            TempData["SuccessMessage"] = $"Stock updated! Added {Quantity} {ingredient.Unit} to {ingredient.Name}.";
            return RedirectToAction("Inventory");
        }

        public async Task<IActionResult> Products()
        {
            var products = await _db.Products.Include(p => p.Category).ToListAsync();
            ViewBag.Categories = await _db.Categories.ToListAsync();
            return View(products);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(Product product, IFormFile photo)
        {
            if (photo != null && photo.Length > 0)
            {
                try
                {
                    var result = await _photoService.AddPhotoAsync(photo);
                    if (result.Error == null && result.SecureUrl != null)
                    {
                        product.ImageURL = result.SecureUrl.AbsoluteUri;
                    }
                    else
                    {
                        var errorMsg = result.Error?.Message ?? "Unknown upload error";
                        TempData["ErrorMessage"] = $"Photo upload failed: {errorMsg}";
                    }
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "Connectivity error: Could not reach Cloudinary. Please check your internet connection.";
                }
            }

            product.IsAvailable = true;
            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            // Automatically create a default base recipe using Actual API Data from the external RecipeService
            bool foundRecipe = false;
            try 
            {
                var apiRecipes = await _recipeService.GetDefaultRecipeFromApiAsync(product.ProductName, product.CategoryID);
                
                if (apiRecipes != null && apiRecipes.Any())
                {
                    foreach (var item in apiRecipes)
                    {
                        _db.ProductRecipes.Add(new ProductRecipe 
                        { 
                            ProductID = product.ProductID,
                            IngredientID = item.IngredientID,
                            QuantityRequired = item.QuantityRequired
                        });
                    }
                    await _db.SaveChangesAsync();
                    foundRecipe = true;
                }
            }
            catch (Exception ex)
            {
                // Non-critical if external API or recipe mapping fails
                Console.WriteLine($"API-based recipe initialization failed: {ex.Message}");
            }

            await LogAudit($"Created Product: {product.ProductName}");
            
            if (foundRecipe)
            {
                TempData["SuccessMessage"] = $"Product '{product.ProductName}' created with an automated recipe!";
            }
            else
            {
                TempData["SuccessMessage"] = $"Product '{product.ProductName}' created, but couldn't find the necessary ingredients in your inventory to build a recipe automatically.";
            }

            return RedirectToAction("Products");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStock(int id, int stock)
        {
            var product = await _db.Products.FindAsync(id);
            if (product != null)
            {
                var diff = stock - product.StockQuantity;
                if (Math.Abs(diff) > 0)
                {
                    _db.InventoryLogs.Add(new InventoryLog
                    {
                        ProductID = product.ProductID,
                        QuantityChange = (decimal)diff,
                        ChangeType = "Correction",
                        LogDate = DateTime.UtcNow,
                        Remarks = "Direct stock update from Product list"
                    });
                }

                product.StockQuantity = stock;
                await _db.SaveChangesAsync();
                await LogAudit($"Updated Stock for {product.ProductName} to {stock}");
                TempData["SuccessMessage"] = "Stock updated successfully!";
            }
            return RedirectToAction("Products");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(Product product, IFormFile photo)
        {
            var existingProduct = await _db.Products.FindAsync(product.ProductID);
            if (existingProduct == null) return NotFound();

            if (photo != null && photo.Length > 0)
            {
                try
                {
                    var result = await _photoService.AddPhotoAsync(photo);
                    if (result.Error == null && result.SecureUrl != null)
                    {
                        existingProduct.ImageURL = result.SecureUrl.AbsoluteUri;
                    }
                    else
                    {
                        var errorMsg = result.Error?.Message ?? "Unknown upload error";
                        TempData["ErrorMessage"] = $"Photo upload failed: {errorMsg}";
                    }
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "Connectivity error: Could not reach Cloudinary. Please check your internet connection.";
                }
            }

            // Log stock change if manually adjusted
            var stockDiff = product.StockQuantity - existingProduct.StockQuantity;
            if (Math.Abs(stockDiff) > 0)
            {
                _db.InventoryLogs.Add(new InventoryLog
                {
                    ProductID = existingProduct.ProductID,
                    QuantityChange = (decimal)stockDiff,
                    ChangeType = "Correction",
                    LogDate = DateTime.UtcNow,
                    Remarks = "Manual adjustment from Edit Product menu"
                });
            }

            existingProduct.ProductName = product.ProductName;
            existingProduct.CategoryID = product.CategoryID;
            existingProduct.Price = product.Price;
            existingProduct.StockQuantity = product.StockQuantity;
            existingProduct.IsAvailable = Request.Form["IsAvailable"] == "true";

            await _db.SaveChangesAsync();
            await LogAudit($"Edited Product: {existingProduct.ProductName}");
            TempData["SuccessMessage"] = "Product updated successfully!";
            return RedirectToAction("Products");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveProduct(int id)
        {
            var product = await _db.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product != null)
            {
                var archived = new ArchivedProduct
                {
                    OriginalProductID = product.ProductID,
                    ProductName = product.ProductName,
                    CategoryID = product.CategoryID,
                    CategoryName = product.Category?.CategoryName,
                    Price = product.Price,
                    ImageURL = product.ImageURL,
                    ArchivedAt = DateTime.UtcNow,
                    Reason = "User requested archive"
                };

                _db.ArchivedProducts.Add(archived);
                _db.Products.Remove(product);
                
                await _db.SaveChangesAsync();
                await LogAudit($"Archived Product: {product.ProductName}");
                TempData["SuccessMessage"] = "Product moved to archives successfully!";
            }
            return RedirectToAction("Products");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            // Keeping for compatibility but favoring Archive
            var product = await _db.Products.FindAsync(id);
            if (product != null)
            {
                var prodName = product.ProductName;
                _db.Products.Remove(product);
                await _db.SaveChangesAsync();
                await LogAudit($"Deleted Product: {prodName}");
                TempData["SuccessMessage"] = "Product deleted successfully!";
            }
            return RedirectToAction("Products");
        }

        public async Task<IActionResult> Inventory()
        {
            var products = await _db.Products.Include(p => p.Category).ToListAsync();
            var ingredients = await _db.Ingredients.OrderBy(i => i.Name).ToListAsync();

            var viewModel = new InventoryViewModel
            {
                Products = products,
                Ingredients = ingredients,
                LowStockCount = ingredients.Count(i => i.StockQuantity > 0 && i.StockQuantity < i.LowStockThreshold),
                OutOfStockCount = ingredients.Count(i => i.StockQuantity <= 0),
                HealthyStockCount = ingredients.Count(i => i.StockQuantity >= i.LowStockThreshold && i.StockQuantity > 0),
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Reports()
        {
            var topProducts = await _db.OrderDetails
                .GroupBy(od => od.Product.ProductName)
                .Select(g => new ProductSalesSummary
                {
                    ProductName = g.Key,
                    TotalSold = g.Sum(od => od.Quantity),
                    Revenue = g.Sum(od => od.Subtotal)
                })
                .OrderByDescending(s => s.TotalSold)
                .Take(10)
                .ToListAsync();

            ViewBag.ChartLabels = topProducts.Select(p => p.ProductName).ToList();
            ViewBag.ChartData = topProducts.Select(p => p.TotalSold).ToList();
            ViewBag.RevenueData = topProducts.Select(p => p.Revenue).ToList();

            return View(topProducts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EmailSalesReport(DateTime? startDate, DateTime? endDate)
        {
            DateTime start = startDate ?? DateTime.Today.AddDays(-30);
            DateTime end = endDate ?? DateTime.UtcNow;

            bool success = await _receiptService.SendSalesReportAsync(start, end);
            
            if (success)
                return Json(new { success = true, message = "Executive summary has been sent to your email." });
            else
                return Json(new { success = false, message = "Failed to generate or send the report. Please check SMTP settings." });
        }

        [HttpGet]
        public async Task<IActionResult> ExportSalesCSV(DateTime? startDate, DateTime? endDate)
        {
            DateTime start = startDate ?? DateTime.Today.AddDays(-30);
            DateTime end = endDate ?? DateTime.UtcNow;

            var data = await _db.OrderDetails
                .Include(od => od.Order)
                .Include(od => od.Product)
                    .ThenInclude(p => p.Category)
                .Where(od => od.Order.OrderDate >= start && od.Order.OrderDate <= end && 
                            (od.Order.PaymentStatus == "Paid" || od.Order.PaymentStatus == "Paid (Digital)" || od.Order.PaymentStatus == "Completed"))
                .GroupBy(od => new { od.Product.ProductName, Category = od.Product.Category != null ? od.Product.Category.CategoryName : "Uncategorized" })
                .Select(g => new
                {
                    Product = g.Key.ProductName,
                    Category = g.Key.Category,
                    TotalSold = g.Sum(od => od.Quantity),
                    Revenue = g.Sum(od => od.Subtotal)
                })
                .OrderByDescending(s => s.TotalSold)
                .ToListAsync();

            var totalRevenue = data.Sum(x => x.Revenue);
            var totalUnits = data.Sum(x => x.TotalSold);

            var csv = new System.Text.StringBuilder();
            // Add UTF-8 BOM for Excel compatibility
            csv.Append('\uFEFF');

            // Business Header
            csv.AppendLine("LJP IT SOLUTIONS - COFFEE ERP");
            csv.AppendLine("SALES PERFORMANCE REPORT");
            csv.AppendLine($"Period: {start:yyyy-MM-dd} to {end:yyyy-MM-dd}");
            csv.AppendLine($"Generated: {DateTime.UtcNow:f}");
            csv.AppendLine();

            // Executive Summary Stats
            csv.AppendLine("EXECUTIVE SUMMARY");
            csv.AppendLine($"Total Gross Revenue,\"₱{totalRevenue:N2}\"");
            csv.AppendLine($"Total Inventory Units Sold,{totalUnits:N0}");
            csv.AppendLine($"Average Sale Value Per Product,\"₱{(totalUnits > 0 ? (totalRevenue / totalUnits) : 0):N2}\"");
            csv.AppendLine();

            // Detailed Data Table
            csv.AppendLine("ITEMIZED PERFORMANCE");
            csv.AppendLine("Product Name,Category,Units Sold,Sales Mix %,Revenue (PHP)");

            foreach (var item in data)
            {
                var salesMix = totalUnits > 0 ? (decimal)item.TotalSold / totalUnits : 0;
                csv.AppendLine($"\"{item.Product}\",\"{item.Category}\",{item.TotalSold},{salesMix:P2},\"{item.Revenue:N2}\"");
            }

            csv.AppendLine();
            csv.AppendLine("--- END OF REPORT ---");

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(buffer, "text/csv", $"LJP_Sales_Report_{start:yyyyMMdd}.csv");
        }

        public async Task<IActionResult> Finance()
        {
            var revenue = await _db.Orders.Where(o => (o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)" || o.PaymentStatus == "Completed")).SumAsync(o => o.FinalAmount);
            var expenses = await _db.Expenses.SumAsync(e => e.Amount);
            
            var last6Months = Enumerable.Range(0, 6).Select(i => DateTime.Today.AddMonths(-5 + i)).ToList();
            var trendLabels = new List<string>();
            var incomeTrend = new List<decimal>();
            var expenseTrend = new List<decimal>();

            foreach (var month in last6Months)
            {
                var start = new DateTime(month.Year, month.Month, 1);
                var end = start.AddMonths(1).AddSeconds(-1);
                
                trendLabels.Add(month.ToString("MMM"));
                incomeTrend.Add(await _db.Orders.Where(o => o.OrderDate >= start && o.OrderDate <= end).SumAsync(o => o.FinalAmount));
                expenseTrend.Add(await _db.Expenses.Where(e => e.ExpenseDate >= start && e.ExpenseDate <= end).SumAsync(e => e.Amount));
            }

            ViewBag.TrendLabels = trendLabels;
            ViewBag.IncomeTrend = incomeTrend;
            ViewBag.ExpenseTrend = expenseTrend;

            var viewModel = new FinanceViewModel
            {
                TotalRevenue = revenue,
                TotalExpenses = expenses,
                Expenses = await _db.Expenses.Include(e => e.User).OrderByDescending(e => e.ExpenseDate).Take(50).ToListAsync(),
                RecentTransactions = await _db.Orders.OrderByDescending(o => o.OrderDate).Take(50).ToListAsync()
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Promotions()
        {
            // Managers only see pending campaigns for approval
            var pendingCampaigns = await _db.Promotions
                .Where(p => p.ApprovalStatus == "Pending")
                .OrderBy(p => p.PromotionID)
                .ToListAsync();
            
            return View(pendingCampaigns);
        }

        public async Task<IActionResult> Marketing()
        {
            var performance = await _db.Orders
                .Where(o => o.PromotionID != null)
                .GroupBy(o => o.Promotion!.PromotionName)
                .Select(g => new { Name = g.Key, Count = g.Count(), Revenue = g.Sum(o => o.FinalAmount) })
                .ToListAsync();

            ViewBag.Performance = performance;
            ViewBag.ActivePromos = await _db.Promotions.CountAsync(p => p.IsActive);
            ViewBag.TotalPromoUses = await _db.Orders.CountAsync(o => o.PromotionID != null);
            ViewBag.TotalPromoRevenue = await _db.Orders.Where(o => o.PromotionID != null).SumAsync(o => o.FinalAmount);
            ViewBag.AvgDiscount = await _db.Orders.Where(o => o.PromotionID != null).AnyAsync() 
                ? await _db.Orders.Where(o => o.PromotionID != null).AverageAsync(o => o.DiscountAmount) 
                : 0;

            // Chart Data
            ViewBag.PromoLabels = performance.Select(p => p.Name).ToList();
            ViewBag.PromoRevenue = performance.Select(p => p.Revenue).ToList();
            ViewBag.PromoCounts = performance.Select(p => p.Count).ToList();

            return View();
        }

        public async Task<IActionResult> Transactions()
        {
            var transactions = await _db.Orders
                .Include(o => o.Cashier)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            return View(transactions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoidOrder(Guid id)
        {
            var order = await _db.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                        .ThenInclude(p => p.Category)
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order != null && order.PaymentStatus != "Voided" && order.PaymentStatus != "Refunded")
            {
                await RevertOrderInventory(order);
                
                // Reverse Loyalty Points
                if (order.CustomerID.HasValue && order.Customer != null)
                {
                    var coffeeCount = order.OrderDetails
                        .Where(d => d.Product != null && d.Product.Category != null && d.Product.Category.CategoryName == "Coffee")
                        .Sum(d => d.Quantity);
                    
                    if (coffeeCount >= 3)
                    {
                        order.Customer.Points -= (coffeeCount / 3);
                        if (order.Customer.Points < 0) order.Customer.Points = 0;
                    }
                }

                order.PaymentStatus = "Voided";
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Transaction voided successfully, inventory restored, and loyalty points deducted!";
                await LogAudit("Voided Order #" + order.OrderID);
            }
            else if (order != null)
            {
                TempData["ErrorMessage"] = "Order is already voided or refunded.";
            }
            return RedirectToAction("Transactions");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefundOrder(Guid id)
        {
            var order = await _db.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                        .ThenInclude(p => p.Category)
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order != null && order.PaymentStatus != "Voided" && order.PaymentStatus != "Refunded")
            {
                await RevertOrderInventory(order);

                // Reverse Loyalty Points
                if (order.CustomerID.HasValue && order.Customer != null)
                {
                    var coffeeCount = order.OrderDetails
                        .Where(d => d.Product != null && d.Product.Category != null && d.Product.Category.CategoryName == "Coffee")
                        .Sum(d => d.Quantity);
                    
                    if (coffeeCount >= 3)
                    {
                        order.Customer.Points -= (coffeeCount / 3);
                        if (order.Customer.Points < 0) order.Customer.Points = 0;
                    }
                }

                order.PaymentStatus = "Refunded";
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Transaction refunded successfully, inventory restored, and loyalty points deducted!";
                await LogAudit("Refunded Order #" + order.OrderID);
            }
            else if (order != null)
            {
                TempData["ErrorMessage"] = "Order is already voided or refunded.";
            }
            return RedirectToAction("Transactions");
        }

        private async Task RevertOrderInventory(Order order)
        {
            if (order == null || order.OrderDetails == null) return;

            foreach (var detail in order.OrderDetails)
            {
                var product = await _db.Products
                    .Include(p => p.ProductRecipes)
                    .ThenInclude(pr => pr.Ingredient)
                    .FirstOrDefaultAsync(p => p.ProductID == detail.ProductID);

                if (product != null)
                {
                    if (product.ProductRecipes != null && product.ProductRecipes.Any())
                    {
                        foreach (var recipe in product.ProductRecipes)
                        {
                            recipe.Ingredient.StockQuantity += (recipe.QuantityRequired * detail.Quantity);
                            
                            // Log ingredient reversal
                            _db.InventoryLogs.Add(new InventoryLog
                            {
                                IngredientID = recipe.IngredientID,
                                QuantityChange = (recipe.QuantityRequired * detail.Quantity),
                                ChangeType = "Reversal",
                                LogDate = DateTime.UtcNow,
                                Remarks = $"Restored from Voided/Refunded Order #{order.OrderID.ToString().Substring(0, 8)} ({product.ProductName})"
                            });
                        }
                    }
                    else
                    {
                        product.StockQuantity += detail.Quantity;
                        
                        _db.InventoryLogs.Add(new InventoryLog
                        {
                            ProductID = product.ProductID,
                            QuantityChange = detail.Quantity,
                            ChangeType = "Reversal",
                            LogDate = DateTime.UtcNow,
                            Remarks = $"Stock restored from Order #{order.OrderID.ToString().Substring(0, 8)}"
                        });
                    }
                }
            }
        }

        // --- EXPENSE CRUD ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateExpense(Expense expense)
        {
            if (ModelState.IsValid)
            {
                expense.CreatedBy = GetCurrentUserId();
                _db.Expenses.Add(expense);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Expense recorded successfully!";
                await LogAudit("Created Expense", expense.Title);
            }
            return RedirectToAction("Finance");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditExpense(Expense expense)
        {
            if (ModelState.IsValid)
            {
                var existing = await _db.Expenses.FindAsync(expense.ExpenseID);
                if (existing != null)
                {
                    existing.Title = expense.Title;
                    existing.Description = expense.Description;
                    existing.Category = expense.Category;
                    existing.Amount = expense.Amount;
                    existing.ExpenseDate = expense.ExpenseDate;
                    
                    _db.Expenses.Update(existing);
                    await _db.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Expense updated successfully!";
                    await LogAudit("Edited Expense: " + expense.Title);
                }
            }
            return RedirectToAction("Finance");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            var expense = await _db.Expenses.FindAsync(id);
            if (expense != null)
            {
                _db.Expenses.Remove(expense);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Expense deleted successfully!";
                await LogAudit("Deleted Expense: " + expense.Title);
            }
            return RedirectToAction("Finance");
        }

        // --- INGREDIENT CRUD ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateIngredient(Ingredient ingredient)
        {
            if (ModelState.IsValid)
            {
                if (ingredient.StockQuantity > 0)
                {
                    ingredient.LastStockedDate = DateTime.UtcNow;
                }

                // STEP 1: Save the ingredient first so it gets a valid IngredientID
                _db.Ingredients.Add(ingredient);
                await _db.SaveChangesAsync();

                // STEP 2: Now that the ingredient has a valid ID, log the initial stock (if any)
                if (ingredient.StockQuantity > 0)
                {
                    _db.InventoryLogs.Add(new InventoryLog
                    {
                        IngredientID = ingredient.IngredientID, // Now has a valid DB-assigned ID
                        QuantityChange = ingredient.StockQuantity,
                        ChangeType = "Initial",
                        LogDate = DateTime.UtcNow,
                        Remarks = "Initial stock upon ingredient creation"
                    });
                    await _db.SaveChangesAsync();
                }

                await LogAudit($"Created Ingredient: {ingredient.Name}");
                TempData["SuccessMessage"] = "Ingredient added successfully!";
            }
            return RedirectToAction("Inventory");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditIngredient(Ingredient ingredient)
        {
            var existing = await _db.Ingredients.FindAsync(ingredient.IngredientID);
            if (existing != null)
            {
                // If stock is being adjusted manually, log the change
                if (Math.Abs(ingredient.StockQuantity - existing.StockQuantity) > 0.0001m)
                {
                    bool isIncrease = ingredient.StockQuantity > existing.StockQuantity;
                    if (isIncrease) existing.LastStockedDate = DateTime.UtcNow;

                    _db.InventoryLogs.Add(new InventoryLog
                    {
                        IngredientID = existing.IngredientID,
                        QuantityChange = ingredient.StockQuantity - existing.StockQuantity,
                        ChangeType = "Correction",
                        LogDate = DateTime.UtcNow,
                        Remarks = "Manual adjustment from Edit menu"
                    });
                }

                existing.Name = ingredient.Name;
                existing.StockQuantity = ingredient.StockQuantity;
                existing.Unit = ingredient.Unit;
                existing.LowStockThreshold = ingredient.LowStockThreshold;
                existing.ExpiryDate = ingredient.ExpiryDate;
                
                // Trigger Persistent Notification if Low Stock
                if (existing.StockQuantity <= existing.LowStockThreshold)
                {
                    var notification = new Notification
                    {
                        Title = "Low Ingredient Stock",
                        Message = $"{existing.Name} needs restocking ({existing.StockQuantity:0.##} {existing.Unit}).",
                        Type = "danger",
                        IconClass = "fas fa-cube",
                        CreatedAt = DateTime.UtcNow,
                        TargetUrl = "/Manager/Inventory"
                    };
                    _db.Notifications.Add(notification);

                    // Email Alert
                    // Send alert in background
                    _ = Task.Run(async () => {
                        try {
                            using (var scope = _scopeFactory.CreateScope()) {
                                var scopedReceiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                                await scopedReceiptService.SendLowStockAlertAsync(existing.IngredientID);
                            }
                        } catch (Exception ex) {
                            Console.WriteLine($"[Email Failure]: {ex.Message}");
                        }
                    });
                }

                await _db.SaveChangesAsync();
                await LogAudit($"Edited Ingredient: {existing.Name}");
                TempData["SuccessMessage"] = "Ingredient updated successfully!";
            }
            return RedirectToAction("Inventory");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteIngredient(int id)
        {
            var ingredient = await _db.Ingredients.FindAsync(id);
            if (ingredient != null)
            {
                var ingName = ingredient.Name;
                _db.Ingredients.Remove(ingredient);
                await _db.SaveChangesAsync();
                await LogAudit($"Deleted Ingredient: {ingName}");
                TempData["SuccessMessage"] = "Ingredient removed successfully!";
            }
            return RedirectToAction("Inventory");
        }

        [HttpGet]
        public async Task<IActionResult> GetIngredientDetails(int id)
        {
            var ingredient = await _db.Ingredients
                .FirstOrDefaultAsync(i => i.IngredientID == id);

            if (ingredient == null) return NotFound();

            var logs = await _db.InventoryLogs
                .Where(l => l.IngredientID == id)
                .OrderByDescending(l => l.LogDate)
                .Take(10)
                .Select(l => new 
                {
                    l.LogDate,
                    l.QuantityChange,
                    l.ChangeType,
                    l.Remarks
                })
                .ToListAsync();

            var result = new 
            {
                ingredient = new 
                {
                    name = ingredient.Name,
                    unit = ingredient.Unit,
                    stockQuantity = ingredient.StockQuantity,
                    lowStockThreshold = ingredient.LowStockThreshold,
                    expiryDate = ingredient.ExpiryDate,
                    lastStockedDate = logs.FirstOrDefault(l => l.ChangeType == "Intake" || l.ChangeType == "Adjustment")?.LogDate ?? ingredient.LastStockedDate
                },
                logs = logs
            };

            return Json(result);
        }

        // --- CAMPAIGN APPROVAL WORKFLOW ---

        public async Task<IActionResult> Recipes()
        {
            var products = await _db.Products
                .Include(p => p.Category)
                .Include(p => p.ProductRecipes)
                    .ThenInclude(pr => pr.Ingredient)
                .ToListAsync();
            
            ViewBag.Ingredients = await _db.Ingredients.ToListAsync();
            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> GetProductRecipes(int productId)
        {
            var recipes = await _db.ProductRecipes
                .Where(pr => pr.ProductID == productId)
                .Select(pr => new 
                { 
                    ingredientID = pr.IngredientID, 
                    quantityRequired = pr.QuantityRequired 
                })
                .ToListAsync();
            
            return Json(recipes);
        }

        public class RecipeItemDto
        {
            public int IngredientID { get; set; }
            public decimal QuantityRequired { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRecipe(int ProductID, List<RecipeItemDto> Recipes)
        {
            // Remove existing recipes for this product
            var existingRecipes = await _db.ProductRecipes
                .Where(pr => pr.ProductID == ProductID)
                .ToListAsync();
            
            _db.ProductRecipes.RemoveRange(existingRecipes);

            // Add new recipes (filter out empty selections)
            if (Recipes != null)
            {
                foreach (var recipe in Recipes.Where(r => r.IngredientID > 0 && r.QuantityRequired > 0))
                {
                    _db.ProductRecipes.Add(new ProductRecipe
                    {
                        ProductID = ProductID,
                        IngredientID = recipe.IngredientID,
                        QuantityRequired = recipe.QuantityRequired
                    });
                }
            }

            await _db.SaveChangesAsync();
            await LogAudit($"Updated Recipe for Product ID: {ProductID}", JsonSerializer.Serialize(Recipes));
            TempData["SuccessMessage"] = "Recipe updated successfully!";
            return RedirectToAction("Recipes");
        }

        [HttpPost]
        public async Task<IActionResult> SyncPaymentStatus(Guid id)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderID == id);
            if (order == null) return NotFound();

            if (order.PaymentStatus == "Pending" || order.PaymentStatus == "Processing")
            {
                // In a real app, this would call PayMongo API to check the source/payment status
                // Simulating a successful sync for demonstration
                order.PaymentStatus = order.PaymentMethod == "Paymongo" ? "Paid (Digital)" : "Paid";
                await _db.SaveChangesAsync();
                await LogAudit($"Manually Synced Payment Status for Order #{order.OrderID.ToString().Substring(0, 8)}", "Status updated from Pending to Paid via Sync Button");
                TempData["SuccessMessage"] = "Payment status synchronized successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Order is already in a final state.";
            }
            return RedirectToAction("Transactions");
        }

        // --- CAMPAIGN APPROVAL WORKFLOW ---
        public async Task<IActionResult> PendingCampaigns()
        {
            var pendingCampaigns = await _db.Promotions
                .Where(p => p.ApprovalStatus == "Pending")
                .OrderBy(p => p.PromotionID)
                .ToListAsync();
            
            return View(pendingCampaigns);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveCampaign(int id)
        {
            var campaign = await _db.Promotions.FindAsync(id);
            if (campaign == null)
                return NotFound();

            campaign.ApprovalStatus = "Approved";
            campaign.ApprovedDate = DateTime.UtcNow;
            campaign.ApprovedBy = GetCurrentUserId();
            
            await _db.SaveChangesAsync();
            await LogAudit($"Approved Campaign: {campaign.PromotionName}");
            
            // Email Notification to Marketing/Admins
            // Send status alert in background
            _ = Task.Run(async () => {
                try {
                    using (var scope = _scopeFactory.CreateScope()) {
                        var scopedReceiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                        await scopedReceiptService.SendPromotionStatusAlertAsync(campaign);
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"[Email Failure]: {ex.Message}");
                }
            });
            
            TempData["SuccessMessage"] = $"Campaign '{campaign.PromotionName}' approved successfully!";
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> RejectCampaign([FromBody] RejectCampaignDto dto)
        {
            var campaign = await _db.Promotions.FindAsync(dto.Id);
            if (campaign == null)
                return NotFound();

            campaign.ApprovalStatus = "Rejected";
            campaign.RejectionReason = dto.Reason;
            campaign.IsActive = false; // Ensure rejected campaigns are not active
            
            await _db.SaveChangesAsync();
            await LogAudit($"Rejected Campaign: {campaign.PromotionName} (Reason: {dto.Reason})");

            // Email Notification to Marketing/Admins
            // Send status alert in background
            _ = Task.Run(async () => {
                try {
                    using (var scope = _scopeFactory.CreateScope()) {
                        var scopedReceiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                        await scopedReceiptService.SendPromotionStatusAlertAsync(campaign);
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"[Email Failure]: {ex.Message}");
                }
            });
            
            TempData["SuccessMessage"] = $"Campaign '{campaign.PromotionName}' rejected.";
            return Ok();
        }

    }

    public class RejectCampaignDto
    {
        public int Id { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
