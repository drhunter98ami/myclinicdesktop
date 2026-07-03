using System.ComponentModel.DataAnnotations;

namespace MyClinic.Models
{
    public class Shortage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Item { get; set; } = string.Empty;

        [Required]
        public bool IsUrgent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
