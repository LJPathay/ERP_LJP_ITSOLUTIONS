using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using ljp_itsolutions.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ljp_itsolutions.Data;
using Microsoft.EntityFrameworkCore;

namespace ljp_itsolutions.Controllers
{
    [Authorize(Roles = "Cashier,Admin,Manager")]
    public class CashierController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly InMemoryStore _store;
        private readonly IPayMongoService _payMongoService;

        public CashierController(ApplicationDbContext db, InMemoryStore store, IPayMongoService payMongoService)
        {
            _db = db;
            _store = store;
            _payMongoService = payMongoService;
        }

        public async Task<IActionResult> CreateOrder()
        {
            var products = await _db.Products.Include(p => p.Category).Where(p => p.IsAvailable).ToListAsync();
            return View(products);
        }

        [HttpPost]
        public async Task<IActionResult> PlaceOrder(List<int> productIds, string paymentMethod = "Cash", int? customerId = null, string? promoCode = null)
        {
            if (productIds == null || !productIds.Any())
                return RedirectToAction("Index", "POS");

            // 1. Get current user ID from claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var cashierId))
                return Challenge();

            // 2. Create the order
            var order = new Order
            {
                OrderID = Guid.NewGuid(),
                OrderDate = DateTime.Now,
                CashierID = cashierId,
                CustomerID = customerId,
                PaymentMethod = paymentMethod,
                PaymentStatus = paymentMethod == "Paymongo" ? "Paid (Digital)" : "Completed"
            };

            // Basic Promotion handling
            if (!string.IsNullOrEmpty(promoCode))
            {
                var promotion = await _db.Promotions.FirstOrDefaultAsync(p => p.PromotionName == promoCode && p.IsActive);
                if (promotion != null)
                {
                    order.PromotionID = promotion.PromotionID;
                }
            }

            // 3. Group IDs to simplify quantity handling
            var productGroups = productIds.GroupBy(id => id);
            decimal total = 0;

            foreach (var group in productGroups)
            {
                var product = await _db.Products
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
                            
                            // Log ingredient usage
                            _db.InventoryLogs.Add(new InventoryLog
                            {
                                ProductID = product.ProductID,
                                QuantityChange = (int)-(recipe.QuantityRequired * qty), // Casting to int for legacy log, though ingredients use decimal
                                ChangeType = "Recipe Deduction",
                                LogDate = DateTime.Now,
                                Remarks = $"Used {recipe.Ingredient.Name} for Order #{order.OrderID.ToString().Substring(0, 8)}"
                            });
                        }
                    }
                    else
                    {
                        // Fallback: update product stock if no recipe defined
                        product.StockQuantity -= qty;
                    }
                }
            }

            order.TotalAmount = total;
            order.FinalAmount = total; // No discount for now

            // 4. Record Payment
            order.Payments.Add(new Payment
            {
                OrderID = order.OrderID,
                PaymentDate = DateTime.Now,
                AmountPaid = total,
                PaymentMethod = paymentMethod,
                PaymentStatus = "Success"
            });

            // 5. Create Inventory Log
            foreach (var detail in order.OrderDetails)
            {
                _db.InventoryLogs.Add(new InventoryLog
                {
                    ProductID = detail.ProductID,
                    QuantityChange = -detail.Quantity,
                    ChangeType = "Sale",
                    LogDate = DateTime.Now,
                    Remarks = $"Order #{order.OrderID.ToString().Substring(0, 8)}"
                });
            }

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            // Notify UI (via TempData for simple redirect)
            TempData["SuccessMessage"] = "Order placed successfully!";
            
            return RedirectToAction("TransactionHistory");
        }

        public async Task<IActionResult> TransactionHistory()
        {
            var orders = await _db.Orders
                .Include(o => o.OrderDetails)
                .Include(o => o.Cashier)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            return View(orders);
        }

        [HttpPost]
        public async Task<IActionResult> CreatePayMongoOrder([FromBody] PayMongoOrderRequest request)
        {
            if (request.ProductIds == null || !request.ProductIds.Any())
                return BadRequest("No products selected.");

            // 1. Get current user ID
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var cashierId))
                return Unauthorized();

            // 2. Create the order
            var order = new Order
            {
                OrderID = Guid.NewGuid(),
                OrderDate = DateTime.Now,
                CashierID = cashierId,
                PaymentMethod = "E-Wallet (Paymongo)",
                PaymentStatus = "Pending"
            };

            // 3. Process products and recipes
            var productGroups = request.ProductIds.GroupBy(id => id);
            decimal total = 0;

            foreach (var group in productGroups)
            {
                var product = await _db.Products
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
                        Quantity = qty,
                        UnitPrice = product.Price,
                        Subtotal = product.Price * qty
                    };
                    order.OrderDetails.Add(detail);
                    total += detail.Subtotal;

                    // Deduct inventory (logic from PlaceOrder)
                    if (product.ProductRecipes != null && product.ProductRecipes.Any())
                    {
                        foreach (var recipe in product.ProductRecipes)
                        {
                            recipe.Ingredient.StockQuantity -= (recipe.QuantityRequired * qty);
                            _db.InventoryLogs.Add(new InventoryLog
                            {
                                ProductID = product.ProductID,
                                QuantityChange = (int)-(recipe.QuantityRequired * qty),
                                ChangeType = "Recipe Deduction",
                                LogDate = DateTime.Now,
                                Remarks = $"Used {recipe.Ingredient.Name} for Order #{order.OrderID.ToString().Substring(0, 8)}"
                            });
                        }
                    }
                }
            }

            order.TotalAmount = total;
            order.FinalAmount = total;

            // 4. Create real PayMongo QR Ph code
            var qrCodeUrl = await _payMongoService.CreateQrPhPaymentAsync(total, $"Order #{order.OrderID.ToString().Substring(0, 8)}", order.OrderID.ToString());

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
        }

        public IActionResult ProcessPayment(Guid orderId, decimal amount)
        {
            // Fully integrated into PlaceOrder for the POS flow
            return RedirectToAction("TransactionHistory");
        }
    }
}
