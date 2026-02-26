using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ljp_itsolutions.Models
{
    public class Product
    {
        [Key]
        public int ProductID { get; set; }

        [Required]
        [StringLength(100)]
        public string ProductName { get; set; } = string.Empty;

        public int CategoryID { get; set; }
        [ForeignKey("CategoryID")]
        public virtual Category Category { get; set; } = null!;

        public decimal Price { get; set; }

        public int StockQuantity { get; set; }
        public int LowStockThreshold { get; set; } = 5;

        public string? ImageURL { get; set; }

        public bool IsAvailable { get; set; } = true;
        public bool IsArchived { get; set; } = false;
        public virtual ICollection<ProductRecipe> ProductRecipes { get; set; } = new List<ProductRecipe>();

        // Legacy compatibility
        [NotMapped] public Guid Id { get; set; } = Guid.NewGuid();
        [NotMapped] public string Name { get => ProductName; set => ProductName = value; }
        [NotMapped] public string Description { get; set; } = string.Empty;
        [NotMapped] public int Stock { get => StockQuantity; set => StockQuantity = value; }
    }
}
