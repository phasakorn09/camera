using System;
using System.Collections.Generic;

namespace ExcelDashboardWebForms
{
    public class Product
    {
        public string ProductId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal SalePrice { get; set; }
        public decimal Cost { get; set; }
        public int Stock { get; set; }
        public decimal ProfitMargin => SalePrice - Cost;
    }

    public class Customer
    {
        public string CustomerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public bool IsMember { get; set; }
        public string FullName => FirstName + " " + LastName;
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
        public decimal TotalAmount { get; set; }
    }

    public class CategorySummary
    {
        public string Category { get; set; }
        public int ProductCount { get; set; }
        public int TotalStock { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class SalesByProductSummary
    {
        public string ProductName { get; set; }
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class SalesByDateSummary
    {
        public string DateLabel { get; set; }
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
    }

    public class DashboardSummary
    {
        public int ProductCount { get; set; }
        public int CustomerCount { get; set; }
        public int EmployeeCount { get; set; }
        public int SaleCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalProfit { get; set; }
        public int TotalStock { get; set; }
        public int MemberCount { get; set; }
        public decimal AverageOrderValue { get; set; }
        public string TopProduct { get; set; }
        public string TopCustomer { get; set; }
    }

    public class DashboardData
    {
        public DashboardSummary Summary { get; set; }
        public List<Product> Products { get; set; }
        public List<Customer> Customers { get; set; }
        public List<Employee> Employees { get; set; }
        public List<Sale> Sales { get; set; }
        public List<CategorySummary> CategorySummaries { get; set; }
        public List<SalesByProductSummary> SalesByProduct { get; set; }
        public List<SalesByDateSummary> SalesByDate { get; set; }
        public string ExcelPath { get; set; }
        public DateTime LoadedAt { get; set; }
    }
}
