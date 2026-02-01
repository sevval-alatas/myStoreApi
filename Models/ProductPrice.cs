using System;

namespace MyStore.Api.Models
{
    public class ProductPrice
    {
        public String Id { get; set; }

        public String MarketId { get; set; }
        public Market Market { get; set; } = null!;

        public String ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public decimal Price { get; set; }
        public int Stock { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}