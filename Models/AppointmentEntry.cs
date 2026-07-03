using System;
using System.ComponentModel.DataAnnotations;

namespace MyClinic.Models
{
    public class AppointmentEntry
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string PatientName { get; set; } = string.Empty;

        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public string Reason { get; set; } = string.Empty;

        public DateTime AppointmentDateTime { get; set; }
    }
}
