using System.Threading.Tasks;
using MyStore.Api.Enums;

public interface IMarketAuthorizationService
{
    Task<MarketRole?> GetUserRoleAsync(string userId, string marketId);
}