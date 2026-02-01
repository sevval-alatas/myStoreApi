using System.Collections.Generic;

namespace MyStore.Api.Models
{
    public class Product
    {
        public string Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;

        public ICollection<ProductPrice> ProductPrices { get; set; } = new List<ProductPrice>();
    }
}