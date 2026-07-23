using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using ModaPanelApi.models;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;

namespace ModaPanelApi.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var expectedUsername = Environment.GetEnvironmentVariable("ADMIN_USERNAME");
            var expectedPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

            if (string.IsNullOrWhiteSpace(expectedUsername) ||
                string.IsNullOrWhiteSpace(expectedPassword))
            {
                return StatusCode(503, new { message = "Yönetici girişi yapılandırılmamış." });
            }

            if (request.Username != expectedUsername || request.Password != expectedPassword)
                return Unauthorized(new { message = "Kullanıcı adı veya şifre yanlış." });

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, expectedUsername),
                new Claim(ClaimTypes.Role, "Admin")
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal
            );

            return Ok(new { message = "Giriş başarılı." });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { message = "Çıkış yapıldı." });
        }

        [HttpGet("me")]
        public IActionResult Me()
        {
            if (!(User.Identity?.IsAuthenticated ?? false))
                return Unauthorized(new { authenticated = false });

            return Ok(new
            {
                authenticated = true,
                username = User.Identity!.Name
            });
        }
    }
}
