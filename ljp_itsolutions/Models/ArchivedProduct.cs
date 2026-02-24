using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ljp_itsolutions.Models
{
    [Table("Archived_Products")]
    public class ArchivedProduct
    {
        [Key]
        public int ArchivedProductID { get; set; }

        public int OriginalProductID { get; set; }

        [Required]
        [StringLength(100)]
        public string ProductName { get; set; } = string.Empty;

        public int CategoryID { get; set; }
        public string? CategoryName { get; set; }

        public decimal Price { get; set; }

        public string? ImageURL { get; set; }

        public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;

        public string? Reason { get; set; }
    }
}
