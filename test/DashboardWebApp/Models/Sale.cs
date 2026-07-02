using System;

namespace DashboardWebApp.Models
{
    public class Sale
    {
        public string SaleId { get; set; }
        public DateTime SaleDate { get; set; }
        public string CustomerId { get; set; }
        public string EmployeeId { get; set; }
        public string ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
