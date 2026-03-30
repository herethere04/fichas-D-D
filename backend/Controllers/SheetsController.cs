using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DnDSheetApi.Data;
using DnDSheetApi.DTOs;
using DnDSheetApi.Models;
using DnDSheetApi.Services;

namespace DnDSheetApi.Controllers;

[ApiController]
[Route("api/sheets")]
public class SheetsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RateLimitService _rateLimiter;

    public SheetsController(AppDbContext db, RateLimitService rateLimiter)
    {
        _db = db;
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// List all character sheets (name + id only).
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll()
    {
        var sheets = await _db.CharacterSheets
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new SheetListItem
            {
                Id = s.Id,
                CharacterName = s.CharacterName,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            })
            .ToListAsync();

        return Ok(sheets);
    }

    /// <summary>
    /// Get full sheet data (PUBLIC - no auth required for viewing).
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var sheet = await _db.CharacterSheets.FindAsync(id);
        if (sheet == null)
            return NotFound(new { message = "Ficha não encontrada." });

        return Ok(new
        {
            sheet.Id,
            sheet.CharacterName,
            sheet.SheetData,
            sheet.CreatedAt,
            sheet.UpdatedAt
        });
    }

    /// <summary>
    /// Create a new character sheet.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateSheetRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var sheet = new CharacterSheet
        {
            CharacterName = request.CharacterName.Trim(),
            EditPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.EditPassword),
            SheetData = "{}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.CharacterSheets.Add(sheet);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = sheet.Id }, new SheetListItem
        {
            Id = sheet.Id,
            CharacterName = sheet.CharacterName,
            CreatedAt = sheet.CreatedAt,
            UpdatedAt = sheet.UpdatedAt
        });
    }

    /// <summary>
    /// Update sheet data (requires edit password).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSheetRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Rate limit: 10 update attempts per minute per IP
        if (_rateLimiter.IsRateLimited($"update:{ip}", 10, TimeSpan.FromMinutes(1)))
            return StatusCode(429, new { message = "Muitas tentativas. Tente novamente em 1 minuto." });

        var sheet = await _db.CharacterSheets.FindAsync(id);
        if (sheet == null)
            return NotFound(new { message = "Ficha não encontrada." });

        if (!BCrypt.Net.BCrypt.Verify(request.EditPassword, sheet.EditPasswordHash))
            return StatusCode(403, new { message = "Senha de edição incorreta." });

        sheet.SheetData = request.SheetData;
        sheet.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Ficha atualizada com sucesso." });
    }

    /// <summary>
    /// Delete a sheet (requires edit password).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id, [FromBody] VerifyPasswordRequest request)
    {
        var sheet = await _db.CharacterSheets.FindAsync(id);
        if (sheet == null)
            return NotFound(new { message = "Ficha não encontrada." });

        if (!BCrypt.Net.BCrypt.Verify(request.EditPassword, sheet.EditPasswordHash))
            return StatusCode(403, new { message = "Senha de edição incorreta." });

        _db.CharacterSheets.Remove(sheet);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Ficha removida com sucesso." });
    }

    /// <summary>
    /// Verify edit password for a sheet (to unlock editing).
    /// </summary>
    [HttpPost("{id}/verify-password")]
    [Authorize]
    public async Task<IActionResult> VerifyPassword(int id, [FromBody] VerifyPasswordRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Rate limit: 10 password verify attempts per minute per IP
        if (_rateLimiter.IsRateLimited($"verify:{ip}:{id}", 10, TimeSpan.FromMinutes(1)))
            return StatusCode(429, new { message = "Muitas tentativas. Tente novamente em 1 minuto." });

        var sheet = await _db.CharacterSheets.FindAsync(id);
        if (sheet == null)
            return NotFound(new { message = "Ficha não encontrada." });

        if (!BCrypt.Net.BCrypt.Verify(request.EditPassword, sheet.EditPasswordHash))
            return StatusCode(403, new { message = "Senha incorreta." });

        return Ok(new { message = "Senha verificada. Edição liberada." });
    }
}
