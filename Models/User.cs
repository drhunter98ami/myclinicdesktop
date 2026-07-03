using System.ComponentModel.DataAnnotations;

namespace MyClinic.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        public string PasswordHash { get; set; } = string.Empty; // Changed name to clarify it's a hash
    }
}
