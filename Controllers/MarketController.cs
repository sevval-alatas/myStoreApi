using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyStore.Api.Data;
using MyStore.Api.Enums;
using MyStore.Api.Models;




namespace MyStore.Api.Controllers
{
    [ApiController]
[Authorize]
[Route("api/markets")]
public class MarketsController : ControllerBase
{
    private readonly AppDbContext _context;

    public MarketsController(AppDbContext context)
    {
        _context = context;
    }

    // ---------------- GET ALL ----------------
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = User.FindFirst("UserId")!.Value;

        var markets = await _context.Markets
            .AsQueryable() // 👈 NETLEŞTİRİYOR
            .Where(m => m.UserMarkets.Any(um => um.UserId == userId))
            .Include(m => m.UserMarkets)
            .ThenInclude(um => um.User)
            .ToListAsync();

        return Ok(markets.Select(m => new
        {
            m.Id,
            m.Name,
            m.Address,
            m.CreatedAt,
            Users = m.UserMarkets.Select(um => new
            {
                um.User.FullName,
                um.Role
            })
        }));
    }


    // ---------------- GET BY ID ----------------
    [HttpGet("{marketId}")]
    public async Task<IActionResult> GetById(string marketId)
    {
        var market = await _context.Markets
            .Include(m => m.UserMarkets)
                .ThenInclude(um => um.User)
            .FirstOrDefaultAsync(m => m.Id == marketId);

        if (market == null) return NotFound();

        return Ok(market);
    }

    // ---------------- CREATE ----------------
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Market market)
    {
        var userId = User.FindFirst("UserId")!.Value;

        market.Id = Guid.NewGuid().ToString();
        market.CreatedAt = DateTime.UtcNow;

        await _context.Markets.AddAsync(market);

        await _context.UserMarkets.AddAsync(new UserMarket
        {
            UserId = userId,
            MarketId = market.Id,
            Role = MarketRole.Owner,
            JoinedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return Ok(market);
    }

    // ---------------- JOIN ----------------
    [HttpPost("{marketId}/join")]
    public async Task<IActionResult> Join(string marketId)
    {
        var userId = User.FindFirst("UserId")!.Value;

        var exists = await _context.UserMarkets
            .AsQueryable()
            .AnyAsync(x => x.MarketId == marketId && x.UserId == userId);


        if (exists) return BadRequest("Zaten bu markettesiniz");

        await _context.UserMarkets.AddAsync(new UserMarket
        {
            UserId = userId,
            MarketId = marketId,
            Role = MarketRole.Member,
            JoinedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return Ok("Market’e katıldınız");
    }

    // ---------------- UPDATE ----------------
    [HttpPut("{marketId}")]
    public async Task<IActionResult> Update(string marketId, Market market)
    {
        if (marketId != market.Id) return BadRequest();

        _context.Markets.Update(market);
        await _context.SaveChangesAsync();
        return Ok(market);
    }

    // ---------------- DELETE ----------------
    [HttpDelete("{marketId}")]
    public async Task<IActionResult> Delete(string marketId)
    {
        var market = await _context.Markets.FindAsync(marketId);
        if (market == null) return NotFound();

        _context.Markets.Remove(market);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}


    

    [ApiController]
    [Authorize]
    [Route("api/markets/{marketId}/invites")]
    public class MarketInviteController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MarketInviteController(AppDbContext context)
        {
            _context = context;
        }

        // -------- CREATE INVITE --------
        [HttpPost]
        public async Task<IActionResult> Generate(string marketId)
        {
            var invite = new MarketInvite
            {
                MarketId = marketId,
                Key = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                ExpireAt = DateTime.UtcNow.AddMinutes(10)
            };

            _context.MarketInvites.Add(invite);
            await _context.SaveChangesAsync();

            return Ok(invite);
        }

        // -------- JOIN WITH INVITE --------
        [HttpPost("join")]
        public async Task<IActionResult> Join([FromBody] string key)
        {
            var userId = User.FindFirst("UserId")!.Value;

            var invite = await _context.MarketInvites
                .AsQueryable()
                .FirstOrDefaultAsync(i => i.Key == key && !i.IsUsed);


            if (invite == null || invite.ExpireAt < DateTime.UtcNow)
                return BadRequest("Geçersiz invite");

            await _context.UserMarkets.AddAsync(new UserMarket
            {
                UserId = userId,
                MarketId = invite.MarketId,
                Role = MarketRole.Member
            });

            invite.IsUsed = true;
            invite.UsedByUserId = userId;

            await _context.SaveChangesAsync();
            return Ok("Market’e katıldınız");
        }
    }

}
