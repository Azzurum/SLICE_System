namespace SLICE_System.Models
{
    public class PaymentResult
    {
        public bool IsSuccess { get; set; }
        public string PaymentMethod { get; set; }
        public string ReferenceNumber { get; set; }
        public string ErrorMessage { get; set; }
    }
}