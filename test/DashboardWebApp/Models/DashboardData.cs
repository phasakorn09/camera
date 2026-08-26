using System;
using System.Collections.Generic;
using System.Linq;

namespace DashboardWebApp.Models
{
    public class DashboardData
    {
        public List<Product> Products { get; set; }
        public List<Customer> Customers { get; set; }
        public List<Employee> Employees { get; set; }
        public List<Sale> Sales { get; set; }
        public string SourceFileName { get; set; }
        public DateTime LoadedAt { get; set; }

        public int TotalProducts
        {
            get { return Products != null ? Products.Count : 0; }
        }

        public int TotalCustomers
        {
            get { return Customers != null ? Customers.Count : 0; }
        }

        public int TotalEmployees
        {
            get { return Employees != null ? Employees.Count : 0; }
        }

        public int TotalSalesRecords
        {
            get { return Sales != null ? Sales.Count : 0; }
        }

        public decimal TotalRevenue
        {
            get { return Sales != null ? Sales.Sum(s => s.TotalAmount) : 0m; }
        }

        public int TotalStockUnits
        {
            get { return Products != null ? Products.Sum(p => p.Stock) : 0; }
        }

        public decimal TotalStockValue
        {
            get { return Products != null ? Products.Sum(p => p.StockValue) : 0m; }
        }

        public int MemberCount
        {
            get { return Customers != null ? Customers.Count(c => c.IsMember) : 0; }
        }

        public IList<Product> LowStockProducts
        {
            get
            {
                if (Products == null)
                {
                    return new List<Product>();
                }

                return Products.Where(p => p.Stock < 50).OrderBy(p => p.Stock).ToList();
            }
        }

        public Dictionary<string, decimal> SalesByCategory
        {
            get
            {
                if (Products == null || Sales == null)
                {
                    return new Dictionary<string, decimal>();
                }

                var productLookup = Products
                    .GroupBy(p => p.ProductId)
                    .ToDictionary(g => g.Key, g => g.First().Category ?? "อื่นๆ");

                return Sales
                    .Where(s => productLookup.ContainsKey(s.ProductId))
                    .GroupBy(s => string.IsNullOrWhiteSpace(productLookup[s.ProductId])
                        ? "อื่นๆ"
                        : productLookup[s.ProductId])
                    .ToDictionary(g => g.Key, g => g.Sum(s => s.TotalAmount));
            }
        }

        public Dictionary<string, int> StockByCategory
        {
            get
            {
                if (Products == null)
                {
                    return new Dictionary<string, int>();
                }

                return Products
                    .GroupBy(p => string.IsNullOrWhiteSpace(p.Category) ? "อื่นๆ" : p.Category)
                    .ToDictionary(g => g.Key, g => g.Sum(p => p.Stock));
            }
        }
    }
}
