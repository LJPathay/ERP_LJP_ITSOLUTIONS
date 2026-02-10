using System;
using System.ComponentModel.DataAnnotations;

namespace ljp_itsolutions.Models
{
    public class Expense
    {
        [Key]
        public int ExpenseID { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public DateTime ExpenseDate { get; set; } = DateTime.Now;

        [Required]
        public string Category { get; set; } = "General"; // Supplies, Utilities, Salary, etc.

        public string? ReferenceNumber { get; set; }
    }
}
