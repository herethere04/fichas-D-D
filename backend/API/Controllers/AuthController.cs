using Microsoft.AspNetCore.Mvc;
using DnDSheetApi.Application.DTOs;
using DnDSheetApi.Application.Interfaces;
using DnDSheetApi.Infrastructure.Security;

namespace DnDSheetApi.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly RateLimitService _rateLimiter;

    public AuthController(IAuthService authService, RateLimitService rateLimiter)
    {
        _authService = authService;
        _rateLimiter = rateLimiter;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Rate limit: 5 login attempts per minute per IP
        if (_rateLimiter.IsRateLimited($"login:{ip}", 5, TimeSpan.FromMinutes(1)))
            return StatusCode(429, new { message = "Muitas tentativas de login. Tente novamente em 1 minuto." });

        var token = await _authService.AuthenticateAsync(request.Username, request.Password);
        
        if (token == null)
            return Unauthorized(new { message = "Usuário ou senha inválidos." });

        return Ok(new { token, username = request.Username });
    }
}
