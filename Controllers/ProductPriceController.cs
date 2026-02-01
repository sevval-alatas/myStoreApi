using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyStore.Api.Data;
using MyStore.Api.Models;

namespace MyStore.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductPricesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ProductPricesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var productPrices = await _context.ProductPrices
                .Include(pp => pp.Market)
                .Include(pp => pp.Product)
                .ToListAsync();
            return Ok(productPrices);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var productPrice = await _context.ProductPrices
                .Include(pp => pp.Market)
                .Include(pp => pp.Product)
                .FirstOrDefaultAsync(pp => pp.Id == id);
            if (productPrice == null) return NotFound();
            return Ok(productPrice);
        }

        [HttpPost]
        public async Task<IActionResult> Create(ProductPrice productPrice)
        {
            _context.ProductPrices.Add(productPrice);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = productPrice.Id }, productPrice);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, ProductPrice productPrice)
        {
            if (id != productPrice.Id) return BadRequest();
            _context.Entry(productPrice).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return Ok(productPrice);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var productPrice = await _context.ProductPrices.FindAsync(id);
            if (productPrice == null) return NotFound();

            _context.ProductPrices.Remove(productPrice);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
