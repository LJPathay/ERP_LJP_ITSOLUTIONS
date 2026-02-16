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

            var viewModel = new ManagerDashboardViewModel
            {
                TotalProducts = await _db.Products.CountAsync(),
                TotalUsers = await _db.Users.CountAsync(),
                TotalOrders = await _db.Orders.CountAsync(),
                TotalRevenue = await _db.Orders.SumAsync(o => o.FinalAmount),
                LowStockIngredients = await _db.Ingredients
                    .Where(i => i.StockQuantity < i.LowStockThreshold)
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

            var viewModel = new InventoryViewModel
            {
                Products = products,
                Ingredients = ingredients,
                LowStockCount = ingredients.Count(i => i.StockQuantity > 0 && i.StockQuantity < i.LowStockThreshold),
                OutOfStockCount = ingredients.Count(i => i.StockQuantity == 0),
                HealthyStockCount = ingredients.Count(i => i.StockQuantity >= i.LowStockThreshold)
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

        public async Task<IActionResult> Finance()
        {
            var revenue = await _db.Orders.SumAsync(o => o.FinalAmount);
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

        // --- EXPENSE CRUD ---
        [HttpPost]
        public async Task<IActionResult> CreateExpense(Expense expense)
        {
            if (ModelState.IsValid)
            {
                _db.Expenses.Add(expense);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Expense recorded successfully!";
            }
            return RedirectToAction("Finance");
        }

        // --- INGREDIENT CRUD ---
        [HttpPost]
        public async Task<IActionResult> CreateIngredient(Ingredient ingredient)
        {
            if (ModelState.IsValid)
            {
                _db.Ingredients.Add(ingredient);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Ingredient added successfully!";
            }
            return RedirectToAction("Inventory");
        }

        [HttpPost]
        public async Task<IActionResult> EditIngredient(Ingredient ingredient)
        {
            var existing = await _db.Ingredients.FindAsync(ingredient.IngredientID);
            if (existing != null)
            {
                existing.Name = ingredient.Name;
                existing.StockQuantity = ingredient.StockQuantity;
                existing.Unit = ingredient.Unit;
                existing.LowStockThreshold = ingredient.LowStockThreshold;
                
                // Trigger Persistent Notification if Low Stock
                if (existing.StockQuantity <= existing.LowStockThreshold)
                {
                    var notification = new Notification
                    {
                        Title = "Low Ingredient Stock",
                        Message = $"{existing.Name} needs restocking ({existing.StockQuantity:0.##} {existing.Unit}).",
                        Type = "danger",
                        IconClass = "fas fa-cube",
                        CreatedAt = DateTime.Now
                    };
                    _db.Notifications.Add(notification);
                }

                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Ingredient updated successfully!";
            }
            return RedirectToAction("Inventory");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteIngredient(int id)
        {
            var ingredient = await _db.Ingredients.FindAsync(id);
            if (ingredient != null)
            {
                _db.Ingredients.Remove(ingredient);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Ingredient removed successfully!";
            }
            return RedirectToAction("Inventory");
        }

        // --- PROMOTION CRUD ---
        [HttpPost]
        public async Task<IActionResult> CreatePromotion(Promotion promotion)
        {
            if (ModelState.IsValid)
            {
                _db.Promotions.Add(promotion);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Promotion campaign created!";
            }
            return RedirectToAction("Promotions");
        }

        [HttpPost]
        public async Task<IActionResult> EditPromotion(Promotion promotion)
        {
            var existing = await _db.Promotions.FindAsync(promotion.PromotionID);
            if (existing != null)
            {
                existing.PromotionName = promotion.PromotionName;
                existing.DiscountType = promotion.DiscountType;
                existing.DiscountValue = promotion.DiscountValue;
                existing.StartDate = promotion.StartDate;
                existing.EndDate = promotion.EndDate;
                existing.IsActive = promotion.IsActive;
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Promotion updated successfully!";
            }
            return RedirectToAction("Promotions");
        }


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
            TempData["SuccessMessage"] = "Recipe updated successfully!";
            return RedirectToAction("Recipes");
        }

        [HttpPost]
        public async Task<IActionResult> DeletePromotion(int id)
        {
            var promotion = await _db.Promotions.FindAsync(id);
            if (promotion != null)
            {
                _db.Promotions.Remove(promotion);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Promotion deleted!";
            }
            return RedirectToAction("Promotions");
        }
    }
}
