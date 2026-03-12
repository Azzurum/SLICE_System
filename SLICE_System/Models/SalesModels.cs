namespace SLICE_System.Models
{
    public class MenuProduct
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public decimal BasePrice { get; set; }
        public string Category { get; set; }

        // NEW: Smart POS Depletion Tracking
        public int MaxCookable { get; set; }
        public bool IsInStock => MaxCookable > 0;

        public string ImagePath { get; set; }
    }

    public class CartItem
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public int Qty { get; set; }
        public decimal TotalPrice => Price * Qty;
    }

    public class SaleRecord
    {
        public int SaleID { get; set; }
        public string ProductName { get; set; }
        public int QuantitySold { get; set; }
        public decimal TotalAmount { get; set; }
        public string ReferenceNumber { get; set; }
        public string PaymentMethod { get; set; }

        // The raw time from the Azure SQL Database (UTC)
        public DateTime TransactionDate { get; set; }

        // FIX: Automatically converts Azure UTC to Philippine Time (UTC+8)
        public DateTime LocalTransactionDate => TransactionDate.AddHours(8);

        public string TransactionStatus { get; set; }
    }
}