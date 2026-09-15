using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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
        private readonly AppDbContext _context;

        public AuthController(IConfiguration configuration, AppDbContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        // 🔹 Login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto login)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Kullanıcıyı mail ile bul
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == login.Email);
            if (user == null)
                return Unauthorized("Mail adresi veya şifre hatalı");

            // Şifre doğrulama
            if (!BCrypt.Net.BCrypt.Verify(login.Password, user.PasswordHash))
                return Unauthorized("Mail adresi veya şifre hatalı");

            var response = await IssueTokensAsync(user);
            return Ok(response);
        }

        // 🔹 Register (basit örnek)
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto register)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (await _context.Users.AnyAsync(u => u.PhoneNumber == register.PhoneNumber))
                return BadRequest("Bu telefon numarası zaten kayıtlı.");

            var user = new User
            {
                FullName = register.FullName,
                PhoneNumber = register.PhoneNumber,
                Email = register.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(register.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Kullanıcı başarıyla kayıt oldu",
                userId = user.Id
            });
        }

        // 🔹 Refresh - access token süresi dolunca yenisini almak için kullanılır.
        // Bilerek [Authorize] YOK: buraya gelindiğinde access token zaten geçersiz
        // olabilir, kimlik doğrulaması refresh token'ın kendisiyle yapılıyor.
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var tokenHash = HashToken(request.RefreshToken);

            var existing = await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

            if (existing == null || !existing.IsActive)
                return Unauthorized("Geçersiz veya süresi dolmuş refresh token");

            // Rotation: kullanılan refresh token'ı iptal et, yenisini üret.
            // Aynı token iki kere kullanılmaya çalışılırsa (çalınmış olabilir) artık işe yaramaz.
            existing.RevokedAt = DateTime.UtcNow;
            existing.RevokedByIp = GetClientIp();

            var response = await IssueTokensAsync(existing.User, replaces: existing);
            return Ok(response);
        }

        // 🔹 Logout - tek bir refresh token'ı (yani tek bir oturumu/cihazı) iptal eder.
        // Bilerek [Authorize] YOK, aynı refresh gibi.
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var tokenHash = HashToken(request.RefreshToken);
            var existing = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

            if (existing != null && existing.RevokedAt == null)
            {
                existing.RevokedAt = DateTime.UtcNow;
                existing.RevokedByIp = GetClientIp();
                await _context.SaveChangesAsync();
            }

            // Token bulunamasa ya da zaten iptal edilmiş olsa bile aynı yanıtı dönüyoruz
            // ki dışarıdan "bu token var mı yok mu" bilgisi sızdırılmasın.
            return NoContent();
        }

        // ---------------- Yardımcı metotlar ----------------

        private async Task<AuthResponse> IssueTokensAsync(User user, RefreshToken replaces = null)
        {
            var (accessToken, accessTokenExpiresAt) = GenerateAccessToken(user);

            var refreshTokenExpiryDays = double.TryParse(
                _configuration["JwtSettings:RefreshTokenExpiryDays"], out var days) ? days : 7;

            var rawRefreshToken = GenerateRefreshTokenValue();

            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = HashToken(rawRefreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiryDays),
                CreatedByIp = GetClientIp()
            };

            _context.RefreshTokens.Add(refreshToken);

            if (replaces != null)
                replaces.ReplacedByTokenHash = refreshToken.TokenHash;

            await _context.SaveChangesAsync();

            return new AuthResponse
            {
                AccessToken = accessToken,
                AccessTokenExpiresAt = accessTokenExpiresAt,
                RefreshToken = rawRefreshToken,
                RefreshTokenExpiresAt = refreshToken.ExpiresAt,
                UserId = user.Id,
                FullName = user.FullName
            };
        }

        private (string token, DateTime expiresAt) GenerateAccessToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:SecretKey"]);
            var expiresAt = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["JwtSettings:ExpiryMinutes"]));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("UserId", user.Id),
                    new Claim("Email", user.Email ?? string.Empty),
                }),
                Expires = expiresAt,
                Issuer = _configuration["JwtSettings:Issuer"],
                Audience = _configuration["JwtSettings:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return (tokenHandler.WriteToken(token), expiresAt);
        }

        // Kriptografik olarak güvenli, tahmin edilemeyen rastgele bir refresh token üretir
        private static string GenerateRefreshTokenValue()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes);
        }

        // Şifre değil ama yine de hassas bir sır: DB'de düz metin tutmak yerine hash'liyoruz
        private static string HashToken(string rawToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToHexString(bytes);
        }

        private string GetClientIp()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
