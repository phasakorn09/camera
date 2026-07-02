using System.Collections.Generic;

namespace tset.Models
{
    public class DashboardSummary
    {
        public int TotalProducts { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalEmployees { get; set; }
        public int TotalSales { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }
        public int LowStockCount { get; set; }
        public int MemberCount { get; set; }

        public List<Product> Products { get; set; }
        public List<Customer> Customers { get; set; }
        public List<Employee> Employees { get; set; }
        public List<Sale> Sales { get; set; }

        public DashboardSummary()
        {
            Products = new List<Product>();
            Customers = new List<Customer>();
            Employees = new List<Employee>();
            Sales = new List<Sale>();
        }
    }
}
