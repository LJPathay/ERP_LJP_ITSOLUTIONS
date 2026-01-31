using Microsoft.AspNetCore.Mvc;
using ljp_itsolutions.Services;
using ljp_itsolutions.Models;

namespace ljp_itsolutions.Controllers
{
    public class CashierController : Controller
    {
        private readonly InMemoryStore _store;

        public CashierController(InMemoryStore store)
        {
            _store = store;
        }

        public IActionResult CreateOrder()
        {
            return View(_store.Products.Values);
        }

        [HttpPost]
        public IActionResult PlaceOrder(Guid cashierId, List<Guid> productIds)
        {
            var order = new Order { CashierId = cashierId };
            foreach (var pid in productIds)
            {
                if (_store.Products.TryGetValue(pid, out var p) && p.Stock > 0)
                {
                    order.Lines.Add(new OrderLine { ProductId = p.Id, ProductName = p.Name, Price = p.Price, Quantity = 1 });
                    p.Stock -= 1;
                }
            }
            _store.Orders[order.Id] = order;
            return RedirectToAction("TransactionHistory");
        }

        public IActionResult TransactionHistory()
        {
            return View(_store.Orders.Values);
        }

        public IActionResult ProcessPayment(Guid orderId, decimal amount)
        {
            // stub: would integrate with payment provider
            return RedirectToAction("TransactionHistory");
        }
    }
}
