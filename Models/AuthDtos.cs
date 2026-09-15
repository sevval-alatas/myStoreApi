using System;
using System.ComponentModel.DataAnnotations;

namespace MyStore.Api.Models
{
    public class LoginDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }
    }


    public class RegisterDto
    {
        [Required]
        public string FullName { get; set; }

        [Required]
        [Phone]
        public string PhoneNumber { get; set; }

        [Required]
        [MinLength(6)]
        public string Password { get; set; }

        [Required]
        [Compare("Password", ErrorMessage = "Şifreler eşleşmiyor")]
        public string ConfirmPassword { get; set; }

        [EmailAddress]
        public string Email { get; set; } // opsiyonel
    }

    public class RefreshRequest
    {
        [Required]
        public string RefreshToken { get; set; }
    }

    public class LogoutRequest
    {
        [Required]
        public string RefreshToken { get; set; }
    }

    // Login ve Refresh aynı şekli döner
    public class AuthResponse
    {
        public string AccessToken { get; set; }
        public DateTime AccessTokenExpiresAt { get; set; }
        public string RefreshToken { get; set; }
        public DateTime RefreshTokenExpiresAt { get; set; }
        public string UserId { get; set; }
        public string FullName { get; set; }
        public bool PhoneConfirmed { get; set; }
        public bool EmailConfirmed { get; set; }
    }

    public class VerifyPhoneRequest
    {
        [Required]
        [Phone]
        public string PhoneNumber { get; set; }

        [Required]
        public string Code { get; set; }
    }

    public class VerifyEmailRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Code { get; set; }
    }

}