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

        public CashierController(ApplicationDbContext db, InMemoryStore store, IPayMongoService payMongoService, ILogger<CashierController> logger, IReceiptService receiptService, IServiceScopeFactory scopeFactory)
            : base(db)
        {
            _store = store;
            _payMongoService = payMongoService;
            _logger = logger;
            _receiptService = receiptService;
            _scopeFactory = scopeFactory;
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
                    promotion = await _db.Promotions.FirstOrDefaultAsync(p => 
                        p.PromotionName == promoCode && 
                        p.IsActive && 
                        p.ApprovalStatus == "Approved" && 
                        p.StartDate <= DateTime.UtcNow && 
                        p.EndDate >= DateTime.UtcNow);
                    
                    if (promotion != null)
                    {
                        order.PromotionID = promotion.PromotionID;
                    }
                }

                // Group IDs to simplify quantity handling
                var productGroups = productIds.GroupBy(id => id);
                decimal total = 0;

                // FIRST PASS: Aggregate and Validate Stock for EVERYTHING in the cart
                var requiredIngredients = new Dictionary<int, (string Name, decimal Quantity, string Unit, decimal CurrentStock)>();
                var standaloneProducts = new Dictionary<int, (string Name, int Quantity, int CurrentStock)>();

                foreach (var group in productGroups)
                {
                    var product = await _db.Products
                        .Include(p => p.ProductRecipes)
                        .ThenInclude(pr => pr.Ingredient)
                        .FirstOrDefaultAsync(p => p.ProductID == group.Key);

                    if (product == null) continue;
                    int qty = group.Count();

                    if (product.ProductRecipes != null && product.ProductRecipes.Any())
                    {
                        foreach (var recipe in product.ProductRecipes)
                        {
                            if (requiredIngredients.ContainsKey(recipe.IngredientID))
                            {
                                var existing = requiredIngredients[recipe.IngredientID];
                                requiredIngredients[recipe.IngredientID] = (existing.Name, existing.Quantity + (recipe.QuantityRequired * qty), existing.Unit, existing.CurrentStock);
                            }
                            else
                            {
                                requiredIngredients[recipe.IngredientID] = (recipe.Ingredient.Name, (recipe.QuantityRequired * qty), recipe.Ingredient.Unit, recipe.Ingredient.StockQuantity);
                            }
                        }
                    }
                    else
                    {
                        if (standaloneProducts.ContainsKey(product.ProductID))
                        {
                            var existing = standaloneProducts[product.ProductID];
                            standaloneProducts[product.ProductID] = (existing.Name, existing.Quantity + qty, existing.CurrentStock);
                        }
                        else
                        {
                            standaloneProducts[product.ProductID] = (product.ProductName, qty, product.StockQuantity);
                        }
                    }
                }

                // VALIDATE INGREDIENTS
                foreach (var entry in requiredIngredients)
                {
                    if (entry.Value.CurrentStock < entry.Value.Quantity)
                    {
                        var msg = $"Insufficient {entry.Value.Name}. Need {entry.Value.Quantity}{entry.Value.Unit}, only {entry.Value.CurrentStock:0.##} left.";
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Json(new { success = false, message = msg });
                        TempData["ErrorMessage"] = msg;
                        return RedirectToAction("CreateOrder");
                    }
                }

                // VALIDATE STANDALONE PRODUCTS
                foreach (var entry in standaloneProducts)
                {
                    if (entry.Value.CurrentStock < entry.Value.Quantity)
                    {
                        var msg = $"Insufficient {entry.Value.Name}. Need {entry.Value.Quantity}, only {entry.Value.CurrentStock} left.";
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Json(new { success = false, message = msg });
                        TempData["ErrorMessage"] = msg;
                        return RedirectToAction("CreateOrder");
                    }
                }

                // SECOND PASS: Process Order Details and Deduct Stock
                foreach (var group in productGroups)
                {
                    var product = await _db.Products
                        .Include(p => p.Category)
                        .Include(p => p.ProductRecipes)
                        .ThenInclude(pr => pr.Ingredient)
                        .FirstOrDefaultAsync(p => p.ProductID == group.Key);

                    if (product != null)
                    {
                        int qty = group.Count();
                        var detail = new OrderDetail
                        {
                            OrderID = order.OrderID,
                            ProductID = product.ProductID,
                            Product = product,
                            Quantity = qty,
                            UnitPrice = product.Price,
                            Subtotal = product.Price * qty
                        };
                        order.OrderDetails.Add(detail);
                        total += detail.Subtotal;

                        // Update Inventory
                        if (product.ProductRecipes != null && product.ProductRecipes.Any())
                        {
                            foreach (var recipe in product.ProductRecipes)
                            {
                                recipe.Ingredient.StockQuantity -= (recipe.QuantityRequired * qty);

                                // Trigger Low Stock Notification
                                if (recipe.Ingredient.StockQuantity <= recipe.Ingredient.LowStockThreshold)
                                {
                                    _db.Notifications.Add(new Notification
                                    {
                                        Title = "Low Ingredient Stock",
                                        Message = $"{recipe.Ingredient.Name} needs restocking ({recipe.Ingredient.StockQuantity:0.##} {recipe.Ingredient.Unit}).",
                                        Type = "danger",
                                        IconClass = "fas fa-cube",
                                        CreatedAt = DateTime.UtcNow,
                                        TargetUrl = "/Manager/Inventory"
                                    });

                                    // Email Alert
                                    // Fire low stock alert in background
                                    _ = Task.Run(async () => {
                                        try {
                                            using (var scope = _scopeFactory.CreateScope()) {
                                                var scopedReceiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                                                await scopedReceiptService.SendLowStockAlertAsync(recipe.IngredientID);
                                            }
                                        } catch (Exception ex) {
                                            _logger.LogError(ex, "Background low stock alert failed for ingredient {Id}", recipe.IngredientID);
                                        }
                                    });
                                }
                                
                                // Log ingredient usage
                                _db.InventoryLogs.Add(new InventoryLog
                                {
                                    IngredientID = recipe.IngredientID,
                                    ProductID = product.ProductID,
                                    QuantityChange = -(recipe.QuantityRequired * qty),
                                    ChangeType = "Recipe Deduction",
                                    LogDate = DateTime.UtcNow,
                                    Remarks = $"Used for {product.ProductName} (Order #{order.OrderID.ToString().Substring(0, 8)})"
                                });
                            }
                        }
                        else
                        {
                            product.StockQuantity -= qty;
                        }
                    }
                }

                // Calculate Promotion Discount
                decimal discount = 0;
                if (promotion != null)
                {
                    if (string.Equals(promotion.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase))
                    {
                        discount = total * (promotion.DiscountValue / 100);
                    }
                    else // Fixed
                    {
                        discount = promotion.DiscountValue;
                    }
                    if (discount > total) discount = total;
                }

                // Reward Point Redemption (5 pts = ₱50 discount)
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

                if (discount > total) discount = total;

                order.TotalAmount = total;
                order.DiscountAmount = discount;
                order.FinalAmount = total - discount;

                // Trigger Notification for High Value Order
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

                // Record Payment (Wait until after discount is applied)
                order.Payments.Add(new Payment
                {
                    OrderID = order.OrderID,
                    PaymentDate = DateTime.UtcNow,
                    AmountPaid = order.FinalAmount,
                    PaymentMethod = paymentMethod,
                    PaymentStatus = "Success"
                });

                // Create Inventory Log for Sold Products
                foreach (var detail in order.OrderDetails)
                {
                    _db.InventoryLogs.Add(new InventoryLog
                    {
                        ProductID = detail.ProductID,
                        QuantityChange = -detail.Quantity,
                        ChangeType = "Sale",
                        LogDate = DateTime.UtcNow,
                        Remarks = $"Order #{order.OrderID.ToString().Substring(0, 8)}"
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
                            customer.Points += (coffeeCount / 3);
                        }
                    }
                }

                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                // Auto-send e-receipt if customer has an email
                if (order.CustomerID.HasValue)
                {
                    // Send receipt in background
                    _ = Task.Run(async () => {
                        try {
                            using (var scope = _scopeFactory.CreateScope()) {
                                var scopedReceiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                                await scopedReceiptService.SendOrderReceiptAsync(order.OrderID);
                            }
                        } catch (Exception ex) {
                            _logger.LogError(ex, "Background receipt sending failed for order {OrderId}", order.OrderID);
                        }
                    });
                }
                
                // Detailed audit log with total
                await LogAudit($"Placed Order #{order.OrderID.ToString().Substring(0, 8)}", $"Total: ₱{order.FinalAmount:N2}, Method: {paymentMethod}, Items: {order.OrderDetails.Count}");

                if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
                {
                    TempData["SuccessMessage"] = "Order placed successfully!";
                }
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, orderId = order.OrderID });
                }

                return RedirectToAction("Receipt", new { id = order.OrderID });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PlaceOrder failed for user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "Server error placing order." });
                throw;
            }
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePayMongoOrder([FromBody] PayMongoOrderRequest request)
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
                promotion = await _db.Promotions.FirstOrDefaultAsync(p => 
                    p.PromotionName == request.PromoCode && 
                    p.IsActive && 
                    p.ApprovalStatus == "Approved" && 
                    p.StartDate <= DateTime.UtcNow && 
                    p.EndDate >= DateTime.UtcNow);
                
                if (promotion != null)
                {
                    order.PromotionID = promotion.PromotionID;
                }
            }

            // Process products and recipes
            var productGroups = request.ProductIds.GroupBy(id => id);
            decimal total = 0;

            // Perform Stock Validation BEFORE processing (Aggregated)
            var requiredIngredients = new Dictionary<int, (string Name, decimal Quantity, string Unit, decimal CurrentStock)>();
            var standaloneProducts = new Dictionary<int, (string Name, int Quantity, int CurrentStock)>();

            foreach (var group in productGroups)
            {
                var product = await _db.Products
                    .Include(p => p.ProductRecipes)
                    .ThenInclude(pr => pr.Ingredient)
                    .FirstOrDefaultAsync(p => p.ProductID == group.Key);

                if (product == null) continue;
                int qty = group.Count();

                if (product.ProductRecipes != null && product.ProductRecipes.Any())
                {
                    foreach (var recipe in product.ProductRecipes)
                    {
                        if (requiredIngredients.ContainsKey(recipe.IngredientID))
                        {
                            var existing = requiredIngredients[recipe.IngredientID];
                            requiredIngredients[recipe.IngredientID] = (existing.Name, existing.Quantity + (recipe.QuantityRequired * qty), existing.Unit, existing.CurrentStock);
                        }
                        else
                        {
                            requiredIngredients[recipe.IngredientID] = (recipe.Ingredient.Name, (recipe.QuantityRequired * qty), recipe.Ingredient.Unit, recipe.Ingredient.StockQuantity);
                        }
                    }
                }
                else
                {
                    if (standaloneProducts.ContainsKey(product.ProductID))
                    {
                        var existing = standaloneProducts[product.ProductID];
                        standaloneProducts[product.ProductID] = (existing.Name, existing.Quantity + qty, existing.CurrentStock);
                    }
                    else
                    {
                        standaloneProducts[product.ProductID] = (product.ProductName, qty, product.StockQuantity);
                    }
                }
            }

            // VALIDATE INGREDIENTS
            foreach (var entry in requiredIngredients)
            {
                if (entry.Value.CurrentStock < entry.Value.Quantity)
                {
                    return BadRequest($"Insufficient {entry.Value.Name}. Need {entry.Value.Quantity}{entry.Value.Unit}, only {entry.Value.CurrentStock:0.##} left.");
                }
            }

            // VALIDATE STANDALONE PRODUCTS
            foreach (var entry in standaloneProducts)
            {
                if (entry.Value.CurrentStock < entry.Value.Quantity)
                {
                    return BadRequest($"Insufficient {entry.Value.Name}. Need {entry.Value.Quantity}, only {entry.Value.CurrentStock} left.");
                }
            }

            foreach (var group in productGroups)
            {
                var product = await _db.Products
                    .Include(p => p.Category)
                    .Include(p => p.ProductRecipes)
                    .ThenInclude(pr => pr.Ingredient)
                    .FirstOrDefaultAsync(p => p.ProductID == group.Key);

                if (product != null)
                {
                    int qty = group.Count();
                    var detail = new OrderDetail
                    {
                        OrderID = order.OrderID,
                        ProductID = product.ProductID,
                        Product = product,
                        Quantity = qty,
                        UnitPrice = product.Price,
                        Subtotal = product.Price * qty
                    };
                    order.OrderDetails.Add(detail);
                    total += detail.Subtotal;

                    // Deduct inventory 
                    if (product.ProductRecipes != null && product.ProductRecipes.Any())
                    {
                        foreach (var recipe in product.ProductRecipes)
                        {
                            recipe.Ingredient.StockQuantity -= (recipe.QuantityRequired * qty);
                            
                            // Trigger Low Stock Notification
                            if (recipe.Ingredient.StockQuantity <= recipe.Ingredient.LowStockThreshold)
                            {
                                _db.Notifications.Add(new Notification
                                {
                                    Title = "Low Ingredient Stock",
                                    Message = $"{recipe.Ingredient.Name} needs restocking ({recipe.Ingredient.StockQuantity:0.##} {recipe.Ingredient.Unit}).",
                                    Type = "danger",
                                    IconClass = "fas fa-cube",
                                    CreatedAt = DateTime.UtcNow,
                                    TargetUrl = "/Manager/Inventory"
                                });

                                // Email Alert
                                // Fire low stock alert in background
                                _ = Task.Run(async () => {
                                    try {
                                        using (var scope = _scopeFactory.CreateScope()) {
                                            var scopedReceiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                                            await scopedReceiptService.SendLowStockAlertAsync(recipe.IngredientID);
                                        }
                                    } catch (Exception ex) {
                                        _logger.LogError(ex, "Background low stock alert failed for ingredient {Id}", recipe.IngredientID);
                                    }
                                });
                            }
                            
                            _db.InventoryLogs.Add(new InventoryLog
                            {
                                IngredientID = recipe.IngredientID,
                                ProductID = product.ProductID,
                                QuantityChange = -(recipe.QuantityRequired * qty),
                                ChangeType = "Recipe Deduction",
                                LogDate = DateTime.UtcNow,
                                Remarks = $"Used for {product.ProductName} (Order #{order.OrderID.ToString().Substring(0, 8)})"
                            });
                        }
                    }
                    else
                    {
                        product.StockQuantity -= qty;
                        
                        // Trigger Low Stock Notification for Standalone Products
                        if (product.StockQuantity <= product.LowStockThreshold)
                        {
                            _db.Notifications.Add(new Notification
                            {
                                Title = "Product Low Stock",
                                Message = $"{product.ProductName} is running low ({product.StockQuantity} left).",
                                Type = "warning",
                                IconClass = "fas fa-coffee",
                                CreatedAt = DateTime.UtcNow,
                                TargetUrl = "/Manager/Products" // Or wherever products are managed
                            });
                            
                            // reuse alert service if available for products
                            // Fire product alert in background
                            _ = Task.Run(async () => {
                                try {
                                    using (var scope = _scopeFactory.CreateScope()) {
                                        var scopedReceiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();
                                        await scopedReceiptService.SendProductLowStockAlertAsync(product.ProductID);
                                    }
                                } catch (Exception ex) {
                                    _logger.LogError(ex, "Background product alert failed for product {Id}", product.ProductID);
                                }
                            }); 
                        }
                    }
                }
            }

            // Calculate Promotion Discount
            decimal discount = 0;
            if (promotion != null)
            {
                if (string.Equals(promotion.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase))
                {
                    discount = total * (promotion.DiscountValue / 100);
                }
                else // Fixed
                {
                    discount = promotion.DiscountValue;
                }
                if (discount > total) discount = total;
            }

            // Reward Point Redemption (5 pts = ₱50 discount)
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

            if (discount > total) discount = total;

            order.TotalAmount = total;
            order.DiscountAmount = discount;
            order.FinalAmount = total - discount;

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

            // Create Inventory Log for Sold Products
            foreach (var detail in order.OrderDetails)
            {
                _db.InventoryLogs.Add(new InventoryLog
                {
                    ProductID = detail.ProductID,
                    QuantityChange = -detail.Quantity,
                    ChangeType = "Sale",
                    LogDate = DateTime.UtcNow,
                    Remarks = $"Order #{order.OrderID.ToString().Substring(0, 8)} (Pending Digital)"
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
                        customer.Points += (coffeeCount / 3);
                    }
                }
            }

            // 4. Create real PayMongo QR Ph code
            var qrCodeUrl = await _payMongoService.CreateQrPhPaymentAsync(order.FinalAmount, $"Order #{order.OrderID.ToString().Substring(0, 8)}", order.OrderID.ToString());

            if (string.IsNullOrEmpty(qrCodeUrl))
            {
                return StatusCode(500, "Failed to generate PayMongo QR code.");
            }

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            return Ok(new { qrCodeUrl, orderId = order.OrderID });
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
                
                decimal expected = currentShift.StartingCash;
                foreach(var o in cashOrders)
                    expected += (o.FinalAmount - o.RefundedAmount);
                
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
    }
}
