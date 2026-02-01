using System;
using System.Collections.Generic;

namespace MyStore.Api.Models
{
    
    public class Market
    {
        public string Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<UserMarket> UserMarkets { get; set; } = new List<UserMarket>();
        public ICollection<MarketInvite> Invites { get; set; } = new List<MarketInvite>();
        public ICollection<ProductPrice> ProductPrices { get; set; } = new List<ProductPrice>();
    }

    public class JoinMarketDto
    {
        public string UserId { get; set; }
        public string MarketId { get; set; } // InviteKey yerine MarketId kullanıyoruz
    }
    public class MarketInvite
    {
        public String Id { get; set; }
        public String MarketId { get; set; }
        public Market Market { get; set; }

        public string Key { get; set; }
        public DateTime ExpireAt { get; set; }

        public bool IsUsed { get; set; } = false;
        public String? UsedByUserId { get; set; }
    }
    
    public class JoinMarketRequest
    {
        public String UserId { get; set; }
        public string Key { get; set; }
    }


}