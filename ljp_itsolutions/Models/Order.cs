using System;
using System.Collections.Generic;

namespace ljp_itsolutions.Models
{
    public class Order
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid CashierId { get; set; }
        public List<OrderLine> Lines { get; set; } = new();
        public decimal Total => CalculateTotal();

        private decimal CalculateTotal()
        {
            decimal sum = 0;
            foreach (var l in Lines)
                sum += l.Price * l.Quantity;
            return sum;
        }
    }

    public class OrderLine
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OrderId { get; set; }
        public Order? Order { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }
}
