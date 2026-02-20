using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ljp_itsolutions.Models
{

    public class Category
    {
        [Key]
        public int CategoryID { get; set; }

        [Required]
        [StringLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }

    public class Customer
    {
        [Key]
        public int CustomerID { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;
 
        [EmailAddress]
        [StringLength(100)]
        public string? Email { get; set; }


        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        public int Points { get; set; } = 0;

        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }

    public class Promotion
    {
        [Key]
        public int PromotionID { get; set; }

        [Required]
        [StringLength(100)]
        public string PromotionName { get; set; } = string.Empty;

        [Required]
        public string DiscountType { get; set; } = "Percentage"; // Percentage or Fixed

        public decimal DiscountValue { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        [Required]
        [StringLength(20)]
        public string ApprovalStatus { get; set; } = "Pending"; // Pending, Approved, Rejected

        public Guid? ApprovedBy { get; set; } 

        public DateTime? ApprovedDate { get; set; }

        [StringLength(500)]
        public string? RejectionReason { get; set; }

        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }

    public class SystemSetting
    {
        [Key]
        public string SettingKey { get; set; } = string.Empty;
        public string SettingValue { get; set; } = string.Empty;
    }

    public class RewardRedemption
    {
        [Key]
        public int RedemptionID { get; set; }

        public int CustomerID { get; set; }
        [ForeignKey("CustomerID")]
        public virtual Customer Customer { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string RewardName { get; set; } = string.Empty;

        public int PointsRedeemed { get; set; }

        public DateTime RedemptionDate { get; set; } = DateTime.Now;
    }
}
