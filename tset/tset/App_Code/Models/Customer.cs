namespace tset.Models
{
    public class Customer
    {
        public string CustomerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string IsMember { get; set; }

        public string FullName
        {
            get { return FirstName + " " + LastName; }
        }
    }
}
