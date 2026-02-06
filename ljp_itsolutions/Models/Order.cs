using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ljp_itsolutions.Models
{
    public class Order
    {
        [Key]
        public Guid OrderID { get; set; } = Guid.NewGuid();

        public DateTime OrderDate { get; set; } = DateTime.Now;

        public Guid CashierID { get; set; }
        [ForeignKey("CashierID")]
        public virtual User Cashier { get; set; } = null!;

        public int? CustomerID { get; set; }
        [ForeignKey("CustomerID")]
        public virtual Customer? Customer { get; set; }

        public int? PromotionID { get; set; }
        [ForeignKey("PromotionID")]
        public virtual Promotion? Promotion { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal FinalAmount { get; set; }

        [StringLength(20)]
        public string PaymentStatus { get; set; } = "Pending";

        [StringLength(50)]
        public string PaymentMethod { get; set; } = "Cash";

        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

        // Legacy compatibility
        [NotMapped] public Guid Id { get => OrderID; set => OrderID = value; }
        [NotMapped] public List<OrderLine> Lines { get; set; } = new();
    }

    public class OrderLine
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OrderId { get; set; }
        public virtual Order? Order { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }
}
