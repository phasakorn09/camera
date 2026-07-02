using System;

namespace ExcelDashboard
{
    public class Product
    {
        public string ProductId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal SalePrice { get; set; }
        public decimal Cost { get; set; }
        public int Stock { get; set; }
    }

    public class Customer
    {
        public string CustomerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string Member { get; set; }

        public string FullName
        {
            get { return FirstName + " " + LastName; }
        }
    }

    public class Employee
    {
        public string EmployeeId { get; set; }
        public string Name { get; set; }
        public string Position { get; set; }
        public decimal Salary { get; set; }
    }

    public class Sale
    {
        public string SaleId { get; set; }
        public DateTime SaleDate { get; set; }
        public string CustomerId { get; set; }
        public string EmployeeId { get; set; }
        public string ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }

    public class DashboardSummary
    {
        public int ProductCount { get; set; }
        public int CustomerCount { get; set; }
        public int EmployeeCount { get; set; }
        public int SaleCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }
        public int LowStockCount { get; set; }
    }
}
