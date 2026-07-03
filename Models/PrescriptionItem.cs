namespace MyClinic.Models
{
    public class PrescriptionItem
    {
        public string MedicineName { get; set; } = string.Empty;
        public string? Dosage { get; set; }
        public string? FrequencyHours { get; set; }
        public string? Timing { get; set; }
        public string? Notes { get; set; }
    }
}
