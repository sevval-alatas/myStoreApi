using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyStore.Api.Data;
using MyStore.Api.Enums;

namespace MyStore.Api.Services;

public class MarketAuthorizationService(AppDbContext context)
    : IMarketAuthorizationService
{
    public async Task<MarketRole?> GetUserRoleAsync(string userId, string marketId)
    {
        return await context.UserMarkets
            .AsQueryable() // 🔥 KRİTİK SATIR
            .Where(x => x.UserId == userId && x.MarketId == marketId)
            .Select(x => x.Role)
            .FirstOrDefaultAsync();
    }
}