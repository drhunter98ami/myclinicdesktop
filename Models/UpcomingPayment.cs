namespace MyClinic.Models
{
    public class UpcomingPayment
    {
        public int Id { get; set; }
        
        /// <summary>
        /// تفاصيل أو وصف الدفعة (لمن أو لأي غرض)
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// قيمة الدفعة
        /// </summary>
        public double Amount { get; set; }
    }
}