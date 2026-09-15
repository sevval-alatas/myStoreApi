using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyStore.Api.Data;
using MyStore.Api.Models;

namespace MyStore.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
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
            return Ok(productPrices.Select(ProductPriceDto.FromEntity));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var productPrice = await _context.ProductPrices
                .Include(pp => pp.Market)
                .Include(pp => pp.Product)
                .FirstOrDefaultAsync(pp => pp.Id == id);
            if (productPrice == null) return NotFound();
            return Ok(ProductPriceDto.FromEntity(productPrice));
        }

        [HttpPost]
        public async Task<IActionResult> Create(ProductPrice productPrice)
        {
            productPrice.Id = Guid.NewGuid().ToString(); // Id her zaman sunucuda üretilir
            productPrice.UpdatedAt = DateTime.UtcNow;
            _context.ProductPrices.Add(productPrice);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = productPrice.Id }, ProductPriceDto.FromEntity(productPrice));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, UpdateProductPriceRequest request)
        {
            var productPrice = await _context.ProductPrices.FindAsync(id);
            if (productPrice == null) return NotFound();

            productPrice.Price = request.Price;
            productPrice.Stock = request.Stock;
            productPrice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(ProductPriceDto.FromEntity(productPrice));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var productPrice = await _context.ProductPrices.FindAsync(id);
            if (productPrice == null) return NotFound();

            _context.ProductPrices.Remove(productPrice);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
