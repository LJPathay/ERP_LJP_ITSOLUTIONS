using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ljp_itsolutions.Models
{
    public class CashShift
    {
        [Key]
        public int CashShiftID { get; set; }

        public Guid CashierID { get; set; }
        [ForeignKey("CashierID")]
        public virtual User? Cashier { get; set; }

        public DateTime StartTime { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(18,2)")]
        public decimal StartingCash { get; set; }

        public DateTime? EndTime { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ExpectedEndingCash { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ActualEndingCash { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Difference { get; set; }

        public bool IsClosed { get; set; } = false;
    }
}
