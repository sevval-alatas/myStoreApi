using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyStore.Api.Data;
using MyStore.Api.Models;
using Microsoft.AspNetCore.Authorization;

namespace MyStore.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Sadece giriş yapmış kullanıcılar
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        // ---------------- GET ALL ----------------
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _context.Users.AsQueryable().ToListAsync(); // .NET 10: System.Linq.AsyncEnumerable ile EF Core arasindaki belirsizligi cozer

            return Ok(users.Select(UserDto.FromEntity));
        }

        // ---------------- GET BY ID ----------------
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _context.Users.AsQueryable().FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound();

            return Ok(UserDto.FromEntity(user));
        }

        // NOT: Kullanıcı oluşturma AuthController.Register üzerinden yapılır
        // (şifre orada BCrypt ile hash'leniyor). Buradaki eski Create action'ı
        // ham User nesnesini body'den bind ediyordu; PasswordHash dahil her alanı
        // client belirleyebiliyordu ve şifre hash'lenmeden kaydediliyordu. Kaldırıldı.

        // ---------------- UPDATE ----------------
        // Bir kullanıcı sadece kendi profilini güncelleyebilir.
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, UpdateUserRequest request)
        {
            var currentUserId = User.FindFirst("UserId")?.Value;
            if (currentUserId != id)
                return Forbid();

            var user = await _context.Users.AsQueryable().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            user.FullName = request.FullName;
            user.PhoneNumber = request.PhoneNumber;
            user.Email = request.Email;

            await _context.SaveChangesAsync();

            return Ok(UserDto.FromEntity(user));
        }

        // ---------------- DELETE ----------------
        // Bir kullanıcı sadece kendi hesabını silebilir.
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var currentUserId = User.FindFirst("UserId")?.Value;
            if (currentUserId != id)
                return Forbid();

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
