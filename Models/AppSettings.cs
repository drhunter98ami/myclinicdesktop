using System.ComponentModel.DataAnnotations;

namespace MyClinic.Models
{
    public class AppSettings
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public decimal UsdToSypRate { get; set; } = 15000; // Default conversion rate

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
