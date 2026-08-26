namespace DashboardWebApp.Models
{
    public class Product
    {
        public string ProductId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal SalePrice { get; set; }
        public decimal Cost { get; set; }
        public int Stock { get; set; }

        public decimal ProfitMargin
        {
            get { return SalePrice - Cost; }
        }

        public decimal StockValue
        {
            get { return SalePrice * Stock; }
        }
    }
}
