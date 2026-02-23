using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ljp_itsolutions.Models
{
    public class Notification
    {
        [Key]
        public int NotificationID { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        [StringLength(20)]
        public string Type { get; set; } = "info"; // success, warning, danger, info

        [StringLength(50)]
        public string IconClass { get; set; } = "fas fa-bell";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;

        [StringLength(255)]
        public string? TargetUrl { get; set; }

        public Guid? UserID { get; set; }
        [ForeignKey("UserID")]
        public virtual User? User { get; set; }
    }
}
