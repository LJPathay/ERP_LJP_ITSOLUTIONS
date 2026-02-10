using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Models;
using ljp_itsolutions.Services;
using Microsoft.AspNetCore.Authorization;
using ljp_itsolutions.Data;
using Microsoft.EntityFrameworkCore;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    public class ManagerController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IPhotoService _photoService;

        public ManagerController(ApplicationDbContext db, IPhotoService photoService)
        {
            _db = db;
            _photoService = photoService;
        }

        public IActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> Dashboard()
        {
            var today = DateTime.Today;
            var lowStockThreshold = 10;

            var viewModel = new ManagerDashboardViewModel
            {
                TotalProducts = await _db.Products.CountAsync(),
                TotalUsers = await _db.Users.CountAsync(),
                TotalOrders = await _db.Orders.CountAsync(),
                TotalRevenue = await _db.Orders.SumAsync(o => o.FinalAmount),
                LowStockProducts = await _db.Products
                    .Where(p => p.StockQuantity < lowStockThreshold)
                    .OrderBy(p => p.StockQuantity)
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

            return View(viewModel);
        }

        public async Task<IActionResult> Products()
        {
            var products = await _db.Products.Include(p => p.Category).ToListAsync();
            ViewBag.Categories = await _db.Categories.ToListAsync();
            return View(products);
        }

        [HttpPost]
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
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Connectivity error: Could not reach Cloudinary. Please check your internet connection.";
                    Console.WriteLine($"Cloudinary Exception: {ex.Message}");
                }
            }

            product.IsAvailable = true;
            _db.Products.Add(product);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Product created successfully!";
            return RedirectToAction("Products");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStock(int id, int stock)
        {
            var product = await _db.Products.FindAsync(id);
            if (product != null)
            {
                product.StockQuantity = stock;
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Stock updated successfully!";
            }
            return RedirectToAction("Products");
        }

        [HttpPost]
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
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Connectivity error: Could not reach Cloudinary. Please check your internet connection.";
                    Console.WriteLine($"Cloudinary Exception: {ex.Message}");
                }
            }

            existingProduct.ProductName = product.ProductName;
            existingProduct.CategoryID = product.CategoryID;
            existingProduct.Price = product.Price;
            existingProduct.StockQuantity = product.StockQuantity;
            existingProduct.IsAvailable = Request.Form["IsAvailable"] == "true";

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Product updated successfully!";
            return RedirectToAction("Products");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product != null)
            {
                _db.Products.Remove(product);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Product deleted successfully!";
            }
            return RedirectToAction("Products");
        }

        public async Task<IActionResult> Inventory()
        {
            var products = await _db.Products.Include(p => p.Category).ToListAsync();
            var ingredients = await _db.Ingredients.ToListAsync();
            var lowStockThreshold = 10;

            var viewModel = new InventoryViewModel
            {
                Products = products,
                Ingredients = ingredients,
                LowStockCount = products.Count(p => p.StockQuantity > 0 && p.StockQuantity < lowStockThreshold) 
                              + ingredients.Count(i => i.StockQuantity > 0 && i.StockQuantity < i.LowStockThreshold),
                OutOfStockCount = products.Count(p => p.StockQuantity == 0)
                                + ingredients.Count(i => i.StockQuantity == 0),
                HealthyStockCount = products.Count(p => p.StockQuantity >= lowStockThreshold)
                                  + ingredients.Count(i => i.StockQuantity >= i.LowStockThreshold)
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

            return View(topProducts);
        }

        public async Task<IActionResult> Finance()
        {
            var revenue = await _db.Orders.SumAsync(o => o.FinalAmount);
            var expenses = await _db.Expenses.SumAsync(e => e.Amount);
            
            var viewModel = new FinanceViewModel
            {
                TotalRevenue = revenue,
                TotalExpenses = expenses,
                Expenses = await _db.Expenses.OrderByDescending(e => e.ExpenseDate).Take(10).ToListAsync(),
                RecentTransactions = await _db.Orders.OrderByDescending(o => o.OrderDate).Take(10).ToListAsync()
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Promotions()
        {
            var promotions = await _db.Promotions.ToListAsync();
            return View(promotions);
        }

        public async Task<IActionResult> Marketing()
        {
            // Simplified marketing performance: Sales by Promotion
            var performance = await _db.Orders
                .Where(o => o.PromotionID != null)
                .GroupBy(o => o.Promotion.PromotionName)
                .Select(g => new { Name = g.Key, Count = g.Count(), Revenue = g.Sum(o => o.FinalAmount) })
                .ToListAsync();

            ViewBag.Performance = performance;
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
        public async Task<IActionResult> VoidOrder(int id)
        {
            var order = await _db.Orders.FindAsync(id);
            if (order != null)
            {
                order.PaymentStatus = "Voided";
                // Optionally restore stock here if needed
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Transaction voided successfully!";
            }
            return RedirectToAction("Transactions");
        }
    }
}
