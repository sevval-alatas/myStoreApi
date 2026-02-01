using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyStore.Api.Data;
using MyStore.Api.Models;

namespace MyStore.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 🔹 Login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto login, [FromServices] AppDbContext context)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Kullanıcıyı mail ile bul
            var user = await context.Users.SingleOrDefaultAsync(u => u.Email == login.Email);
            if (user == null)
                return Unauthorized("Mail adresi veya şifre hatalı");

            // Şifre doğrulama
            if (!BCrypt.Net.BCrypt.Verify(login.Password, user.PasswordHash))
                return Unauthorized("Mail adresi veya şifre hatalı");

            // Token oluştur
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:SecretKey"]);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("UserId", user.Id),
                    new Claim("Email", user.Email),
                   
                }),
                Expires = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["JwtSettings:ExpiryMinutes"])),
                Issuer = _configuration["JwtSettings:Issuer"],
                Audience = _configuration["JwtSettings:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);

            return Ok(new
            {
                token = tokenHandler.WriteToken(token),
                expiration = token.ValidTo,
                userId = user.Id,
                fullName = user.FullName
            });
        }

        // 🔹 Register (basit örnek)
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto register, [FromServices] AppDbContext context)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (await context.Users.AnyAsync(u => u.PhoneNumber == register.PhoneNumber))
                return BadRequest("Bu telefon numarası zaten kayıtlı.");

            var user = new User
            {
                FullName = register.FullName,
                PhoneNumber = register.PhoneNumber,
                Email = register.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(register.Password)
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            return Ok(new
            {
                message = "Kullanıcı başarıyla kayıt oldu",
                userId = user.Id
            });
        }
    }



}
