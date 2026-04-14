using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using ModaPanelApi.models;
using System.Security.Claims;

namespace ModaPanelApi.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request.Username != "admin" || request.Password != "ElanurGece33")
                return Unauthorized(new { message = "Kullanıcı adı veya şifre yanlış." });

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, "admin"),
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