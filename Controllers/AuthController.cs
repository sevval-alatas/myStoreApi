using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MyStore.Api.Data;
using MyStore.Api.Enums;
using MyStore.Api.Models;
using MyStore.Api.Services;

namespace MyStore.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private const int VerificationCodeExpiryMinutes = 15;

        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailSender _emailSender;
        private readonly ISmsSender _smsSender;

        public AuthController(
            IConfiguration configuration,
            AppDbContext context,
            IWebHostEnvironment env,
            IEmailSender emailSender,
            ISmsSender smsSender)
        {
            _configuration = configuration;
            _context = context;
            _env = env;
            _emailSender = emailSender;
            _smsSender = smsSender;
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

            // Not: email/telefon doğrulanmamış olsa bile girişe izin veriliyor
            // (bilinçli ürün kararı) - AuthResponse'taki PhoneConfirmed/EmailConfirmed
            // alanlarına bakarak client istersen kullanıcıyı uyarabilir.
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

            // Telefon zorunlu alan olduğu için her zaman bir doğrulama kodu üretilir
            var phoneCode = await GenerateVerificationCodeAsync(user, VerificationType.Phone);
            await _smsSender.SendAsync(user.PhoneNumber, $"MyStore doğrulama kodunuz: {phoneCode}");

            // Email opsiyonel - sadece verildiyse kod üret/gönder
            string emailCode = null;
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                emailCode = await GenerateVerificationCodeAsync(user, VerificationType.Email);
                await _emailSender.SendAsync(user.Email, "MyStore e-posta doğrulama", $"Doğrulama kodunuz: {emailCode}");
            }

            return Ok(new
            {
                message = "Kullanıcı başarıyla kayıt oldu. Telefon numaranızı doğrulamanız öneriliyor.",
                userId = user.Id,
                phoneVerificationRequired = true,
                emailVerificationRequired = !string.IsNullOrWhiteSpace(user.Email),
                // GEÇİCİ: gerçek bir SMS/email sağlayıcısı bağlanana kadar, test edebilmen
                // için kodlar SADECE Development ortamında response'ta da dönüyor.
                // Gerçek sağlayıcı eklenince bu iki alan tamamen kaldırılmalı.
                devPhoneVerificationCode = _env.IsDevelopment() ? phoneCode : null,
                devEmailVerificationCode = _env.IsDevelopment() ? emailCode : null
            });
        }

        // 🔹 Telefon doğrulama
        [HttpPost("verify-phone")]
        public async Task<IActionResult> VerifyPhone([FromBody] VerifyPhoneRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _context.Users.SingleOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
            if (user == null)
                return BadRequest("Geçersiz telefon numarası veya kod");

            var verification = await _context.VerificationCodes
                .FirstOrDefaultAsync(v => v.UserId == user.Id && v.Type == VerificationType.Phone && v.Code == request.Code);

            if (verification == null || !verification.IsActive)
                return BadRequest("Geçersiz veya süresi dolmuş kod");

            verification.UsedAt = DateTime.UtcNow;
            user.PhoneConfirmed = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Telefon numarası doğrulandı" });
        }

        // 🔹 Email doğrulama
        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
                return BadRequest("Geçersiz email veya kod");

            var verification = await _context.VerificationCodes
                .FirstOrDefaultAsync(v => v.UserId == user.Id && v.Type == VerificationType.Email && v.Code == request.Code);

            if (verification == null || !verification.IsActive)
                return BadRequest("Geçersiz veya süresi dolmuş kod");

            verification.UsedAt = DateTime.UtcNow;
            user.EmailConfirmed = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Email doğrulandı" });
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
                FullName = user.FullName,
                PhoneConfirmed = user.PhoneConfirmed,
                EmailConfirmed = user.EmailConfirmed
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

        // 6 haneli, kriptografik olarak güvenli rastgele bir doğrulama kodu üretir ve kaydeder
        private async Task<string> GenerateVerificationCodeAsync(User user, VerificationType type)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

            var verification = new VerificationCode
            {
                UserId = user.Id,
                Type = type,
                Code = code,
                ExpiresAt = DateTime.UtcNow.AddMinutes(VerificationCodeExpiryMinutes)
            };

            _context.VerificationCodes.Add(verification);
            await _context.SaveChangesAsync();

            return code;
        }

        private string GetClientIp()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
