using Microsoft.EntityFrameworkCore;
using MyStore.Api.Models;

namespace MyStore.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Market> Markets { get; set; }
        public DbSet<Product> Products { get; set; }
        
        public DbSet<MarketInvite> MarketInvites { get; set; }

        public DbSet<ProductPrice> ProductPrices { get; set; }
        public DbSet<UserMarket> UserMarkets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------------------------
            // USER <-> MARKET (MANY-TO-MANY)
            // ---------------------------
            modelBuilder.Entity<UserMarket>()
                .HasKey(um => new { um.UserId, um.MarketId });

            modelBuilder.Entity<UserMarket>()
                .HasOne(um => um.User)
                .WithMany(u => u.UserMarkets)
                .HasForeignKey(um => um.UserId);

            modelBuilder.Entity<UserMarket>()
                .HasOne(um => um.Market)
                .WithMany(m => m.UserMarkets)
                .HasForeignKey(um => um.MarketId);


            // ---------------------------
            // MARKET -> MARKET INVITE (1-M)
            // ---------------------------
            modelBuilder.Entity<Market>()
                .HasMany(m => m.Invites)
                .WithOne(i => i.Market)
                .HasForeignKey(i => i.MarketId);
            
            modelBuilder.Entity<MarketInvite>()
                .Property(m => m.Id)
                .ValueGeneratedOnAdd();



            // ---------------------------
            // PRODUCTPRICE -> MARKET (1-M)
            // ---------------------------
            modelBuilder.Entity<ProductPrice>()
                .HasOne(pp => pp.Market)
                .WithMany(m => m.ProductPrices)
                .HasForeignKey(pp => pp.MarketId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---------------------------
            // PRODUCTPRICE -> PRODUCT (1-M)
            // ---------------------------
            modelBuilder.Entity<ProductPrice>()
                .HasOne(pp => pp.Product)
                .WithMany(p => p.ProductPrices)
                .HasForeignKey(pp => pp.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---------------------------
            // UNIQUE: Her market bir ürün için tek fiyat
            // ---------------------------
            modelBuilder.Entity<ProductPrice>()
                .HasIndex(pp => new { pp.MarketId, pp.ProductId })
                .IsUnique();
        }

    }
}