using System;
using System.ComponentModel.DataAnnotations;

namespace ljp_itsolutions.Models
{
    public class User
    {
        public User() { }

        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Username { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        public string? Password { get; set; }

        public string FullName { get; set; } = string.Empty;

        public Role? Role { get; set; }

        public bool? IsArchived { get; set; } = false;
    }
}
