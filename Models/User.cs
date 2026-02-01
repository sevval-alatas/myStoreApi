using System;
using System.Collections.Generic;
using MyStore.Api.Enums;

namespace MyStore.Api.Models
{
    // Models/User.cs
    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public bool PhoneConfirmed { get; set; } = false;
        public bool EmailConfirmed { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<UserMarket> UserMarkets { get; set; }
    }
    
    public class UserMarket
    {
        public string UserId { get; set; }   // Kullanıcıya ait foreign key
        public User User { get; set; }       // Navigasyon property

        public String  MarketId { get; set; }    // Market foreign key
        public Market Market { get; set; }   // Navigasyon property

        public MarketRole Role { get; set; } // ✅ ENUM
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }

}