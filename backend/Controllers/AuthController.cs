using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DnDSheetApi.Data;
using DnDSheetApi.DTOs;
using DnDSheetApi.Services;

namespace DnDSheetApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly RateLimitService _rateLimiter;

    public AuthController(AppDbContext db, IConfiguration config, RateLimitService rateLimiter)
    {
        _db = db;
        _config = config;
        _rateLimiter = rateLimiter;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Rate limit: 5 login attempts per minute per IP
        if (_rateLimiter.IsRateLimited($"login:{ip}", 5, TimeSpan.FromMinutes(1)))
            return StatusCode(429, new { message = "Muitas tentativas de login. Tente novamente em 1 minuto." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Usuário ou senha inválidos." });

        var token = GenerateJwtToken(user.Username);
        return Ok(new { token, username = user.Username });
    }

    private string GenerateJwtToken(string username)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _config["Jwt:Key"] ?? "DnDSheetApiSuperSecretKeyThatIsAtLeast32Chars!"));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "DnDSheetApi",
            audience: _config["Jwt:Audience"] ?? "DnDSheetApp",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
