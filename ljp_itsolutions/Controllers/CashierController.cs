using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using ljp_itsolutions.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ljp_itsolutions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "Cashier,Admin,Manager,SuperAdmin")]
    public class CashierController : BaseController
    {
        private readonly InMemoryStore _store;
        private readonly IPayMongoService _payMongoService;
        private readonly ILogger<CashierController> _logger;
        private readonly IReceiptService _receiptService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAnalyticsService _analyticsService;

        public CashierController(ApplicationDbContext db, InMemoryStore store, IPayMongoService payMongoService, ILogger<CashierController> logger, IReceiptService receiptService, IServiceScopeFactory scopeFactory, IAnalyticsService analyticsService)
            : base(db)
        {
            _store = store;
            _payMongoService = payMongoService;
            _logger = logger;
            _receiptService = receiptService;
            _scopeFactory = scopeFactory;
            _analyticsService = analyticsService;
        }

        public class OrderRequest
        {
            public List<int>? ProductIds { get; set; }
            public string PaymentMethod { get; set; } = "Cash";
            public int? CustomerId { get; set; }
            public string? PromoCode { get; set; }
            public bool RedeemPoints { get; set; }
            public decimal? CashReceived { get; set; }
        }

        public class CustomerRequest
        {
            public string FullName { get; set; } = string.Empty;
            public string? PhoneNumber { get; set; }
            public string? Email { get; set; }
        }

        public IActionResult CreateOrder()
        {
            return RedirectToAction("Index", "POS");
        }

        [HttpGet]
        public async Task<IActionResult> ValidatePromoCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return Json(new { success = false, message = "Empty code" });

            var cleanCode = code.Replace(" ", "").Trim().ToLower();

            var allPromotions = await _db.Promotions.Include(p => p.Orders).ToListAsync();
            var promotion = allPromotions.FirstOrDefault(p => 
                (p.PromotionName ?? "").Replace(" ", "").Equals(cleanCode, StringComparison.OrdinalIgnoreCase));

            if (promotion == null)
            {
                return Json(new { success = false, message = "Invalid promotion code." });
            }

            if (!promotion.IsActive)
            {
                return Json(new { success = false, message = "This promotion is no longer active." });
            }

            if (promotion.ApprovalStatus != "Approved")
            {
                return Json(new { success = false, message = "This promotion is pending manager approval." });
            }

            var today = DateTime.Today; 
            if (promotion.StartDate.Date > today)
            {
                return Json(new { success = false, message = $"This promotion starts on {promotion.StartDate:MMM dd, yyyy}." });
            }

            if (promotion.EndDate.Date < today)
            {
                return Json(new { success = false, message = "This promotion has expired." });
            }

            int currentUsages = promotion.Orders.Count;
            if (promotion.MaxRedemptions.HasValue && currentUsages >= promotion.MaxRedemptions.Value)
            {
                return Json(new { success = false, message = "Promotion fully redeemed." });
            }

            string discountLabel = promotion.DiscountType.ToLower() == "percentage" 
                ? $"{promotion.DiscountValue:0.##}% Off" 
                : $"₱{promotion.DiscountValue:N2} Off";

            return Json(new { 
                success = true, 
                discountType = promotion.DiscountType, 
                discountValue = promotion.DiscountValue, 
                message = $"Promo applied: {discountLabel}" 
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder([FromBody] OrderRequest request)
        {
            var productIds = request.ProductIds;
            var paymentMethod = request.PaymentMethod;
            var customerId = request.CustomerId;
            var promoCode = request.PromoCode;

            if (productIds == null || !productIds.Any())
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "No products selected." });
                return RedirectToAction("Index", "POS");
            }

            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IActionResult>(async () => 
            {
                using var transaction = await _db.Database.BeginTransactionAsync();
                try 
                {
                    //Get current user ID from claims
                    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!Guid.TryParse(userIdClaim, out var cashierId))
                        return Challenge();

                    // Ensure active shift
                    var currentShift = await _db.CashShifts.FirstOrDefaultAsync(s => s.CashierID == cashierId && !s.IsClosed);
                    if (currentShift == null)
                    {
                        var msg = "No open shift found. Please start a shift first.";
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                            return Json(new { success = false, message = msg });
                        TempData["ErrorMessage"] = msg;
                        return RedirectToAction("ShiftManagement");
                    }

                    //Create the order
                    var order = new Order
                    {
                        OrderID = Guid.NewGuid(),
                        OrderDate = DateTime.UtcNow,
                        CashierID = cashierId,
                        CustomerID = customerId,
                        PaymentMethod = paymentMethod,
                        PaymentStatus = paymentMethod == "Paymongo" ? "Paid (Digital)" : "Paid"
                    };

                    Promotion? promotion = null;
                    if (!string.IsNullOrEmpty(promoCode))
                    {
                        var cleanPromo = promoCode.Replace(" ", "").Trim().ToLower();
                        var allPromos = await _db.Promotions.Include(p => p.Orders).ToListAsync();
                        promotion = allPromos.FirstOrDefault(p =>
                            (p.PromotionName ?? "").Replace(" ", "").Equals(cleanPromo, StringComparison.OrdinalIgnoreCase) &&
                            p.IsActive &&
                            p.ApprovalStatus == "Approved" &&
                            p.StartDate.Date <= DateTime.Today &&
                            p.EndDate.Date >= DateTime.Today);

                        if (promotion != null)
                        {
                            int currentUsages = promotion.Orders.Count;

                            // 1. Check total redemption cap
                            if (promotion.MaxRedemptions.HasValue && currentUsages >= promotion.MaxRedemptions.Value)
                            {
                                var msg = $"Sorry, this promotion has reached its maximum redemption limit ({promotion.MaxRedemptions.Value} uses).";
                                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                                    return Json(new { success = false, message = msg });
                                TempData["ErrorMessage"] = msg;
                                return RedirectToAction("CreateOrder");
                            }

                            // 2. Check one-time-per-customer rule
                            if (promotion.OneTimePerCustomer && customerId.HasValue)
                            {
                                bool alreadyUsed = promotion.Orders.Any(o => o.CustomerID == customerId.Value);
                                if (alreadyUsed)
                                {
                                    var msg = "You have already used this promotion code. It can only be used once per customer.";
                                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                                        return Json(new { success = false, message = msg });
                                    TempData["ErrorMessage"] = msg;
                                    return RedirectToAction("CreateOrder");
                                }
                            }

                            order.PromotionID = promotion.PromotionID;
                        }
                    }

                    // Group IDs to simplify quantity handling
                    var productGroups = productIds.GroupBy(id => id);
                    decimal total = 0;

                    // ATOMIC PASS: Fetch, Validate, and Prepare Deductions
                    var orderProducts = new List<(Product Product, int Quantity)>();
                    var requiredIngredients = new Dictionary<int, (Ingredient Ingredient, decimal Quantity)>();
                    var standaloneProducts = new Dictionary<int, (Product Product, int Quantity)>();

                    foreach (var group in productGroups)
                    {
                        var product = await _db.Products
                            .Include(p => p.Category)
                            .Include(p => p.ProductRecipes)
                            .ThenInclude(pr => pr.Ingredient)
                            .FirstOrDefaultAsync(p => p.ProductID == group.Key);

                        if (product == null) continue;
                        int qty = group.Count();
                        orderProducts.Add((product, qty));

                        if (product.ProductRecipes != null && product.ProductRecipes.Any())
                        {
                            foreach (var recipe in product.ProductRecipes)
                            {
                                if (requiredIngredients.ContainsKey(recipe.IngredientID))
                                {
                                    var existing = requiredIngredients[recipe.IngredientID];
                                    requiredIngredients[recipe.IngredientID] = (existing.Ingredient, existing.Quantity + (recipe.QuantityRequired * qty));
                                }
                                else
                                {
                                    requiredIngredients[recipe.IngredientID] = (recipe.Ingredient, (recipe.QuantityRequired * qty));
                                }
                            }
                        }
                        else
                        {
                            if (standaloneProducts.ContainsKey(product.ProductID))
                            {
                                var existing = standaloneProducts[product.ProductID];
                                standaloneProducts[product.ProductID] = (existing.Product, existing.Quantity + qty);
                            }
                            else
                            {
                                standaloneProducts[product.ProductID] = (product, qty);
                            }
                        }
                    }

                    // VALIDATE & DEDUCT IN ONE GO 
                    foreach (var entry in requiredIngredients)
                    {
                        if (entry.Value.Ingredient.StockQuantity < entry.Value.Quantity)
                        {
                            var msg = $"Insufficient {entry.Value.Ingredient.Name}. Need {entry.Value.Quantity}{entry.Value.Ingredient.Unit}, only {entry.Value.Ingredient.StockQuantity:0.##} left.";
                            await transaction.RollbackAsync();
                            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Json(new { success = false, message = msg });
                            TempData["ErrorMessage"] = msg;
                            return RedirectToAction("CreateOrder");
                        }
                        
                        var globalThresholdSetting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "LowStockThreshold");
                        decimal globalThreshold = 5; 
                        if (globalThresholdSetting != null && decimal.TryParse(globalThresholdSetting.SettingValue, out var gVal))
                        {
                            globalThreshold = gVal;
                        }

                        // Deduct
                        entry.Value.Ingredient.StockQuantity -= entry.Value.Quantity;
                        
                        // Log and Notify
                        decimal threshold = entry.Value.Ingredient.LowStockThreshold > 0 
                            ? (decimal)entry.Value.Ingredient.LowStockThreshold 
                            : globalThreshold;

                        if (entry.Value.Ingredient.StockQuantity <= threshold)
                        {
                            _db.Notifications.Add(new Notification
                            {
                                Title = "Low Ingredient Stock",
                                Message = $"{entry.Value.Ingredient.Name} needs restocking ({entry.Value.Ingredient.StockQuantity:0.##} {entry.Value.Ingredient.Unit}).",
                                Type = "danger",
                                IconClass = "fas fa-cube",
                                CreatedAt = DateTime.UtcNow,
                                TargetUrl = "/Manager/Inventory"
                            });
                        }
                    }

                    foreach (var entry in standaloneProducts)
                    {
                        if (entry.Value.Product.StockQuantity < entry.Value.Quantity)
                        {
                            var msg = $"Insufficient {entry.Value.Product.ProductName}. Need {entry.Value.Quantity}, only {entry.Value.Product.StockQuantity} left.";
                            await transaction.RollbackAsync();
                            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Json(new { success = false, message = msg });
                            TempData["ErrorMessage"] = msg;
                            return RedirectToAction("CreateOrder");
                        }
                        
                        // Deduct
                        entry.Value.Product.StockQuantity -= entry.Value.Quantity;
                    }

                    // Create Order Details and Logs
                    foreach (var item in orderProducts)
                    {
                        var detail = new OrderDetail
                        {
                            OrderID = order.OrderID,
                            ProductID = item.Product.ProductID,
                            Quantity = item.Quantity,
                            UnitPrice = item.Product.Price,
                            Subtotal = item.Product.Price * item.Quantity
                        };
                        order.OrderDetails.Add(detail);
                        total += detail.Subtotal;

                        // Log ingredient usage if applicable
                        if (item.Product.ProductRecipes != null && item.Product.ProductRecipes.Any())
                        {
                            foreach (var recipe in item.Product.ProductRecipes)
                            {
                                _db.InventoryLogs.Add(new InventoryLog
                                {
                                    IngredientID = recipe.IngredientID,
                                    ProductID = item.Product.ProductID,
                                    QuantityChange = -(recipe.QuantityRequired * item.Quantity),
                                    ChangeType = "Recipe Deduction",
                                    LogDate = DateTime.UtcNow,
                                    Remarks = $"Used for {item.Product.ProductName} (Order #{order.OrderID.ToString().Substring(0, 8)})"
                                });
                            }
                        }
                        else 
                        {
                            _db.InventoryLogs.Add(new InventoryLog
                            {
                                ProductID = item.Product.ProductID,
                                QuantityChange = -item.Quantity,
                                ChangeType = "Sale",
                                LogDate = DateTime.UtcNow,
                                Remarks = $"Order #{order.OrderID.ToString().Substring(0, 8)}"
                            });
                        }
                    }

                    // Calculate Promotion Discount
                    decimal discount = 0;
                    if (promotion != null)
                    {
                        if (string.Equals(promotion.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase))
                            discount = total * (promotion.DiscountValue / 100);
                        else 
                            discount = promotion.DiscountValue;
                        
                        if (discount > total) discount = total;
                        order.PromotionID = promotion.PromotionID;
                        
                        if (promotion.IsOneTimeReward) promotion.IsActive = false;
                    }

                    // Reward Point Redemption
                    if (request.RedeemPoints && order.CustomerID.HasValue)
                    {
                        var customer = await _db.Customers.FindAsync(order.CustomerID.Value);
                        if (customer != null && customer.Points >= 5)
                        {
                            customer.Points -= 5;
                            discount += 50;
                        }
                    }

                    if (discount > total) discount = total;

                    // Fetch dynamic tax rate
                    var taxSetting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "TaxRate");
                    decimal taxRateValue = 0.05m;
                    if (taxSetting != null && decimal.TryParse(taxSetting.SettingValue, out var parsedTax))
                    {
                        taxRateValue = parsedTax / 100m;
                    }

                    decimal taxableAmount = total - discount;
                    decimal taxAmount = taxableAmount * taxRateValue;

                    order.TotalAmount = total;
                    order.DiscountAmount = discount;
                    order.FinalAmount = taxableAmount + taxAmount;

                    // High Value Notification
                    if (order.FinalAmount >= 500)
                    {
                        _db.Notifications.Add(new Notification
                        {
                            Title = "High Value Order",
                            Message = $"Order #{order.OrderID.ToString().Substring(0, 8)} for ₱{order.FinalAmount:N2} received!",
                            Type = "success",
                            IconClass = "fas fa-star",
                            CreatedAt = DateTime.UtcNow,
                            TargetUrl = "/Manager/Transactions"
                        });
                    }

                    // Payment Record
                    order.Payments.Add(new Payment
                    {
                        OrderID = order.OrderID,
                        PaymentDate = DateTime.UtcNow,
                        AmountPaid = order.FinalAmount,
                        PaymentMethod = paymentMethod,
                        PaymentStatus = "Success"
                    });

                    // Loyalty Points Earning
                    if (order.CustomerID.HasValue)
                    {
                        var customer = await _db.Customers.FindAsync(order.CustomerID.Value);
                        if (customer != null)
                        {
                            var coffeeCount = order.OrderDetails
                                .Where(d => d.Product != null && d.Product.Category != null && d.Product.Category.CategoryName == "Coffee")
                                .Sum(d => d.Quantity);
                            
                            if (coffeeCount >= 3)
                            {
                                decimal multiplier = customer.Points > 1000 ? 1.5m : (customer.Points > 500 ? 1.25m : 1.0m);
                                customer.Points += (int)((coffeeCount / 3) * multiplier);
                            }
                        }
                    }

                    _db.Orders.Add(order);
                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Fire-and-forget background tasks (Post-Commit)
                    _ = Task.Run(async () => {
                        try {
                            using (var scope = _scopeFactory.CreateScope()) {
                                var scopedReceiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                                if (order.CustomerID.HasValue) await scopedReceiptService.SendOrderReceiptAsync(order.OrderID);
                                
                                // Check which ingredients hit low stock and send alerts
                                foreach (var entry in requiredIngredients)
                                {
                                    if (entry.Value.Ingredient.StockQuantity <= entry.Value.Ingredient.LowStockThreshold)
                                        await scopedReceiptService.SendLowStockAlertAsync(entry.Key);
                                }
                            }
                        } catch (Exception ex) {
                            _logger.LogError(ex, "Background processing failed for order {OrderId}", order.OrderID);
                        }
                    });

                    await LogAudit($"Placed Order #{order.OrderID.ToString().Substring(0, 8)}", $"Total: ₱{order.FinalAmount:N2}, Items: {order.OrderDetails.Count}");

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = true, orderId = order.OrderID });

                    TempData["SuccessMessage"] = "Order placed successfully!";
                    return RedirectToAction("Receipt", new { id = order.OrderID });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "PlaceOrder failed for user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "Server error placing order." });
                    throw;
                }
            });
        }

        public async Task<IActionResult> TransactionHistory()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var cashierId))
                return Challenge();

            var orders = await _db.Orders
                .Include(o => o.OrderDetails)
                .Include(o => o.Cashier)
                .Where(o => o.CashierID == cashierId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            return View(orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePayMongoOrder([FromBody] PayMongoOrderRequest request)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IActionResult>(async () => 
            {
                using var transaction = await _db.Database.BeginTransactionAsync();
                try 
                {
                    if (request.ProductIds == null || !request.ProductIds.Any())
                        return BadRequest("No products selected.");

                    //  Get current user ID
                    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!Guid.TryParse(userIdClaim, out var cashierId))
                        return Unauthorized();

                    // Ensure active shift
                    var currentShift = await _db.CashShifts.FirstOrDefaultAsync(s => s.CashierID == cashierId && !s.IsClosed);
                    if (currentShift == null)
                    {
                        return BadRequest("No open shift found. Please start a shift first.");
                    }

                    // Create the order
                    var order = new Order
                    {
                        OrderID = Guid.NewGuid(),
                        OrderDate = DateTime.UtcNow,
                        CashierID = cashierId,
                        CustomerID = request.CustomerId,
                        PaymentMethod = "E-Wallet (Paymongo)",
                        PaymentStatus = "Pending"
                    };

                    Promotion? promotion = null;
                    if (!string.IsNullOrEmpty(request.PromoCode))
                    {
                        var cleanPromo = request.PromoCode.Replace(" ", "").Trim().ToLower();
                        var allPromos = await _db.Promotions.Include(p => p.Orders).ToListAsync();
                        promotion = allPromos.FirstOrDefault(p =>
                            (p.PromotionName ?? "").Replace(" ", "").Equals(cleanPromo, StringComparison.OrdinalIgnoreCase) &&
                            p.IsActive &&
                            p.ApprovalStatus == "Approved" &&
                            p.StartDate.Date <= DateTime.Today &&
                            p.EndDate.Date >= DateTime.Today);

                        if (promotion != null)
                        {
                            int currentUsages = promotion.Orders.Count;

                            // 1. Check total redemption cap
                            if (promotion.MaxRedemptions.HasValue && currentUsages >= promotion.MaxRedemptions.Value)
                            {
                                return BadRequest($"Sorry, this promotion reached its maximum redemption limit ({promotion.MaxRedemptions.Value} uses).");
                            }

                            // 2. Check one-time-per-customer rule
                            if (promotion.OneTimePerCustomer && request.CustomerId.HasValue)
                            {
                                bool alreadyUsed = promotion.Orders.Any(o => o.CustomerID == request.CustomerId.Value);
                                if (alreadyUsed)
                                {
                                    return BadRequest("You have already used this promotion code.");
                                }
                            }

                            order.PromotionID = promotion.PromotionID;
                        }
                    }

                    // Group IDs to simplify quantity handling
                    var productGroups = request.ProductIds.GroupBy(id => id);
                    decimal total = 0;

                    // ATOMIC PASS: Fetch, Validate, and Prepare Deductions
                    var orderProducts = new List<(Product Product, int Quantity)>();
                    var requiredIngredients = new Dictionary<int, (Ingredient Ingredient, decimal Quantity)>();
                    var standaloneProducts = new Dictionary<int, (Product Product, int Quantity)>();

                    foreach (var group in productGroups)
                    {
                        var product = await _db.Products
                            .Include(p => p.Category)
                            .Include(p => p.ProductRecipes)
                            .ThenInclude(pr => pr.Ingredient)
                            .FirstOrDefaultAsync(p => p.ProductID == group.Key);

                        if (product == null) continue;
                        int qty = group.Count();
                        orderProducts.Add((product, qty));

                        if (product.ProductRecipes != null && product.ProductRecipes.Any())
                        {
                            foreach (var recipe in product.ProductRecipes)
                            {
                                if (requiredIngredients.ContainsKey(recipe.IngredientID))
                                {
                                    var existing = requiredIngredients[recipe.IngredientID];
                                    requiredIngredients[recipe.IngredientID] = (existing.Ingredient, existing.Quantity + (recipe.QuantityRequired * qty));
                                }
                                else
                                {
                                    requiredIngredients[recipe.IngredientID] = (recipe.Ingredient, (recipe.QuantityRequired * qty));
                                }
                            }
                        }
                        else
                        {
                            if (standaloneProducts.ContainsKey(product.ProductID))
                            {
                                var existing = standaloneProducts[product.ProductID];
                                standaloneProducts[product.ProductID] = (existing.Product, existing.Quantity + qty);
                            }
                            else
                            {
                                standaloneProducts[product.ProductID] = (product, qty);
                            }
                        }
                    }

                    // VALIDATE & DEDUCT IN ONE GO 
                    foreach (var entry in requiredIngredients)
                    {
                        if (entry.Value.Ingredient.StockQuantity < entry.Value.Quantity)
                        {
                            return BadRequest($"Insufficient {entry.Value.Ingredient.Name}. Need {entry.Value.Quantity}{entry.Value.Ingredient.Unit}, only {entry.Value.Ingredient.StockQuantity:0.##} left.");
                        }
                        
                        var globalThresholdSetting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "LowStockThreshold");
                        decimal globalThreshold = 5; 
                        if (globalThresholdSetting != null && decimal.TryParse(globalThresholdSetting.SettingValue, out var gVal))
                        {
                            globalThreshold = gVal;
                        }

                        // Deduct
                        entry.Value.Ingredient.StockQuantity -= entry.Value.Quantity;
                        
                        // Log and Notify
                        decimal threshold = entry.Value.Ingredient.LowStockThreshold > 0 
                            ? (decimal)entry.Value.Ingredient.LowStockThreshold 
                            : globalThreshold;

                        if (entry.Value.Ingredient.StockQuantity <= threshold)
                        {
                            _db.Notifications.Add(new Notification
                            {
                                Title = "Low Ingredient Stock",
                                Message = $"{entry.Value.Ingredient.Name} needs restocking ({entry.Value.Ingredient.StockQuantity:0.##} {entry.Value.Ingredient.Unit}).",
                                Type = "danger",
                                IconClass = "fas fa-cube",
                                CreatedAt = DateTime.UtcNow,
                                TargetUrl = "/Manager/Inventory"
                            });
                        }
                    }

                    foreach (var entry in standaloneProducts)
                    {
                        if (entry.Value.Product.StockQuantity < entry.Value.Quantity)
                        {
                            return BadRequest($"Insufficient {entry.Value.Product.ProductName}. Need {entry.Value.Quantity}, only {entry.Value.Product.StockQuantity} left.");
                        }
                        
                        // Deduct
                        entry.Value.Product.StockQuantity -= entry.Value.Quantity;
                    }

                    // Create Order Details and Logs
                    foreach (var item in orderProducts)
                    {
                        var detail = new OrderDetail
                        {
                            OrderID = order.OrderID,
                            ProductID = item.Product.ProductID,
                            Quantity = item.Quantity,
                            UnitPrice = item.Product.Price,
                            Subtotal = item.Product.Price * item.Quantity
                        };
                        order.OrderDetails.Add(detail);
                        total += detail.Subtotal;

                        // Log ingredient usage if applicable
                        if (item.Product.ProductRecipes != null && item.Product.ProductRecipes.Any())
                        {
                            foreach (var recipe in item.Product.ProductRecipes)
                            {
                                _db.InventoryLogs.Add(new InventoryLog
                                {
                                    IngredientID = recipe.IngredientID,
                                    ProductID = item.Product.ProductID,
                                    QuantityChange = -(recipe.QuantityRequired * item.Quantity),
                                    ChangeType = "Recipe Deduction",
                                    LogDate = DateTime.UtcNow,
                                    Remarks = $"Used for {item.Product.ProductName} (Order #{order.OrderID.ToString().Substring(0, 8)})"
                                });
                            }
                        }
                        else 
                        {
                            _db.InventoryLogs.Add(new InventoryLog
                            {
                                ProductID = item.Product.ProductID,
                                QuantityChange = -item.Quantity,
                                ChangeType = "Sale",
                                LogDate = DateTime.UtcNow,
                                Remarks = $"Order #{order.OrderID.ToString().Substring(0, 8)}"
                            });
                        }
                    }

                    // Calculate Promotion Discount
                    decimal discount = 0;
                    if (promotion != null)
                    {
                        if (string.Equals(promotion.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase))
                            discount = total * (promotion.DiscountValue / 100);
                        else 
                            discount = promotion.DiscountValue;
                        
                        if (discount > total) discount = total;
                        order.PromotionID = promotion.PromotionID;
                        
                        if (promotion.IsOneTimeReward) promotion.IsActive = false;
                    }

                    // Reward Point Redemption
                    if (request.RedeemPoints && order.CustomerID.HasValue)
                    {
                        var customer = await _db.Customers.FindAsync(order.CustomerID.Value);
                        if (customer != null && customer.Points >= 5)
                        {
                            customer.Points -= 5;
                            discount += 50;
                            await LogAudit($"Redeemed 5 Points", $"Customer: {customer.FullName}, Discount: ₱50.00");
                        }
                    }

                    // Automatic 5% Elite Patron Discount
                    if (discount == 0 && order.CustomerID.HasValue)
                    {
                        var customer = await _db.Customers.FindAsync(order.CustomerID.Value);
                        if (customer != null && customer.Points > 1000)
                        {
                            discount = total * 0.05m;
                        }
                    }

                    if (discount > total) discount = total;

                    // Fetch dynamic tax rate
                    var taxSetting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "TaxRate");
                    decimal taxRateValue = 0.05m;
                    if (taxSetting != null && decimal.TryParse(taxSetting.SettingValue, out var parsedTax))
                    {
                        taxRateValue = parsedTax / 100m;
                    }

                    decimal taxableAmount = total - discount;
                    decimal taxAmount = taxableAmount * taxRateValue;

                    order.TotalAmount = total;
                    order.DiscountAmount = discount;
                    order.FinalAmount = taxableAmount + taxAmount;

                    // Trigger Notification for Price Order (Digital)
                    if (order.FinalAmount >= 500)
                    {
                        _db.Notifications.Add(new Notification
                        {
                            Title = "High Value Order (Digital)",
                            Message = $"Online Order #{order.OrderID.ToString().Substring(0, 8)} for ₱{order.FinalAmount:N2} pending payment.",
                            Type = "info",
                            IconClass = "fas fa-star",
                            CreatedAt = DateTime.UtcNow,
                            TargetUrl = "/Manager/Transactions"
                        });
                    }

                    // Customer Loyalty Points EARNING (1 pt per 3 coffees)
                    if (order.CustomerID.HasValue)
                    {
                        var customer = await _db.Customers.FindAsync(order.CustomerID.Value);
                        if (customer != null)
                        {
                            var coffeeCount = order.OrderDetails
                                .Where(d => d.Product != null && d.Product.Category != null && d.Product.Category.CategoryName == "Coffee")
                                .Sum(d => d.Quantity);
                            
                            if (coffeeCount >= 3)
                            {
                                decimal multiplier = customer.Points > 1000 ? 1.5m : (customer.Points > 500 ? 1.25m : 1.0m);
                                customer.Points += (int)((coffeeCount / 3) * multiplier);
                            }
                        }
                    }

                    // Create real PayMongo QR Ph code
                    var qrCodeUrl = await _payMongoService.CreateQrPhPaymentAsync(order.FinalAmount, $"Order #{order.OrderID.ToString().Substring(0, 8)}", order.OrderID.ToString());

                    if (string.IsNullOrEmpty(qrCodeUrl))
                    {
                        return StatusCode(500, "Failed to generate PayMongo QR code.");
                    }

                    _db.Orders.Add(order);
                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new { qrCodeUrl, orderId = order.OrderID });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "CreatePayMongoOrder failed");
                    return StatusCode(500, "Internal Server Error during PayMongo order initialization.");
                }
            });
        }

        public class PayMongoOrderRequest
        {
            public List<int> ProductIds { get; set; } = new();
            public string? PromoCode { get; set; }
            public int? CustomerId { get; set; }
            public bool RedeemPoints { get; set; }
        }

        public async Task<IActionResult> Receipt(Guid id)
        {
            var order = await _db.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                .Include(o => o.Cashier)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order == null)
                return NotFound();

            return View(order);
        }

        public async Task<IActionResult> GetReceiptPartial(Guid id)
        {
            try 
            {
                var order = await _db.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(d => d.Product)
                    .Include(o => o.Cashier)
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.OrderID == id);

                if (order == null)
                    return NotFound();

                return PartialView("_ReceiptPartial", order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetReceiptPartial for Order ID {OrderId}", id);
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchCustomers(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Json(new List<object>());

            var customers = await _db.Customers
                .Where(c => c.FullName.Contains(query) || (c.PhoneNumber != null && c.PhoneNumber.Contains(query)))
                .Take(5)
                .Select(c => new { c.CustomerID, c.FullName, c.PhoneNumber, c.Points, c.Email })
                .ToListAsync();

            return Json(customers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterCustomer([FromBody] CustomerRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
                return Json(new { success = false, message = "Name is required." });

            var customer = new Customer
            {
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                Points = 0
            };

            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
            await LogAudit($"Registered Customer: {customer.FullName}");

            return Json(new { success = true, customerId = customer.CustomerID, fullName = customer.FullName });
        }

        public IActionResult ProcessPayment(Guid orderId, decimal amount)
        {
            // Fully integrated into PlaceOrder for the POS flow
            return RedirectToAction("TransactionHistory");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendReceipt(Guid orderId, string email)
        {
            if (string.IsNullOrEmpty(email)) return Json(new { success = false, message = "Email is required." });

            bool sent = await _receiptService.SendOrderReceiptAsync(orderId, email);
            
            if (sent) 
                return Json(new { success = true });
            else
                return Json(new { success = false, message = "Failed to send email. Ensure the order exists and email is valid." });
        }

        [HttpGet]
        public async Task<IActionResult> ShiftManagement()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var cashierId))
                return Challenge();

            var currentShift = await _db.CashShifts.FirstOrDefaultAsync(s => s.CashierID == cashierId && !s.IsClosed);
            
            if (currentShift != null)
            {
                var cashOrders = await _db.Orders
                    .Where(o => o.CashierID == cashierId && o.OrderDate >= currentShift.StartTime && o.PaymentMethod == "Cash" && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Completed" || o.PaymentStatus == "Partially Refunded"))
                    .ToListAsync();
                
                decimal sales = cashOrders.Sum(o => o.FinalAmount - o.RefundedAmount);
                decimal expected = currentShift.StartingCash + sales;
                
                ViewBag.CashSales = sales;
                ViewBag.ExpectedCash = expected;
            }
            
            return View(currentShift);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartShift(decimal startingCash)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var cashierId))
                return Challenge();

            var existing = await _db.CashShifts.FirstOrDefaultAsync(s => s.CashierID == cashierId && !s.IsClosed);
            if(existing != null)
            {
                TempData["ErrorMessage"] = "You already have an open shift.";
                return RedirectToAction("ShiftManagement");
            }

            var shift = new CashShift
            {
                CashierID = cashierId,
                StartTime = DateTime.UtcNow,
                StartingCash = startingCash,
                IsClosed = false
            };

            _db.CashShifts.Add(shift);
            await _db.SaveChangesAsync();
            await LogAudit($"Started Shift", $"Float: ₱{startingCash:N2}");

            TempData["SuccessMessage"] = "Shift started successfully! Register is Open.";
            return RedirectToAction("Index", "POS");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CloseShift(decimal actualEndingCash)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var cashierId))
                return Challenge();

            var shift = await _db.CashShifts.FirstOrDefaultAsync(s => s.CashierID == cashierId && !s.IsClosed);
            if(shift == null)
            {
                TempData["ErrorMessage"] = "No open shift found.";
                return RedirectToAction("ShiftManagement");
            }

            var cashOrders = await _db.Orders
                .Where(o => o.CashierID == cashierId && o.OrderDate >= shift.StartTime && o.PaymentMethod == "Cash" && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Completed" || o.PaymentStatus == "Partially Refunded"))
                .ToListAsync();

            decimal expected = shift.StartingCash;
            foreach(var o in cashOrders)
                expected += (o.FinalAmount - o.RefundedAmount);

            shift.ExpectedEndingCash = expected;
            shift.ActualEndingCash = actualEndingCash;
            shift.Difference = actualEndingCash - expected;
            shift.EndTime = DateTime.UtcNow;
            shift.IsClosed = true;

            await _db.SaveChangesAsync();
            
            // Send Shift Report in background to avoid blocking the UI
            _ = Task.Run(async () => {
                try {
                    using (var scope = _scopeFactory.CreateScope()) {
                        var scopedReceiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                        await scopedReceiptService.SendShiftReportAsync(shift.CashShiftID);
                    }
                } catch (Exception ex) {
                    _logger.LogError(ex, "Background shift report sending failed for shift {ShiftId}", shift.CashShiftID);
                }
            });

            await LogAudit($"Closed Shift", $"Expected: ₱{expected:N2}, Actual: ₱{actualEndingCash:N2}, Difference: ₱{shift.Difference:N2}");

            TempData["SuccessMessage"] = $"Shift closed successfully. Difference: ₱{shift.Difference:N2}";
            return RedirectToAction("ShiftManagement");
        }

        [HttpGet]
        public async Task<IActionResult> ExportTransactions()
        {
            byte[] buffer = await _analyticsService.GenerateTransactionsCSVAsync();
            return File(buffer, "text/csv", $"LJP_Transactions_{DateTime.Now:yyyyMMdd}.csv");
        }
    }
}
