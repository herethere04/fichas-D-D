using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DnDSheetApi.Application.DTOs;
using DnDSheetApi.Application.Interfaces;
using DnDSheetApi.Domain.Entities;
using DnDSheetApi.Infrastructure.Security;

namespace DnDSheetApi.API.Controllers;

[ApiController]
[Route("api/sheets")]
public class SheetsController : ControllerBase
{
    private readonly ISheetService _sheetService;
    private readonly RateLimitService _rateLimiter;

    public SheetsController(ISheetService sheetService, RateLimitService rateLimiter)
    {
        _sheetService = sheetService;
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// List all character sheets (name + id only).
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll()
    {
        var sheets = await _sheetService.GetAllSheetsAsync();
        
        var list = sheets.Select(s => new SheetListItem
        {
            Id = s.Id,
            CharacterName = s.CharacterName,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        });

        return Ok(list);
    }

    /// <summary>
    /// Get full sheet data (PUBLIC - no auth required for viewing).
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var sheet = await _sheetService.GetSheetByIdAsync(id);
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

        var sheet = await _sheetService.CreateSheetAsync(request.CharacterName.Trim(), request.EditPassword);

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

        var success = await _sheetService.UpdateSheetAsync(id, request.EditPassword, request.SheetData);
        
        if (!success)
            return StatusCode(403, new { message = "Senha de edição incorreta ou ficha não encontrada." });

        return Ok(new { message = "Ficha atualizada com sucesso." });
    }

    /// <summary>
    /// Delete a sheet (requires edit password).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id, [FromBody] VerifyPasswordRequest request)
    {
        var success = await _sheetService.DeleteSheetAsync(id, request.EditPassword);
        
        if (!success)
            return StatusCode(403, new { message = "Senha de edição incorreta ou ficha não encontrada." });

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

        var isValid = await _sheetService.VerifyPasswordAsync(id, request.EditPassword);
        
        if (!isValid)
            return StatusCode(403, new { message = "Senha incorreta ou ficha não encontrada." });

        return Ok(new { message = "Senha verificada. Edição liberada." });
    }
}
