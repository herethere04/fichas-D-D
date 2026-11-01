using Microsoft.EntityFrameworkCore;
using DnDSheetApi.Domain.Entities;
using DnDSheetApi.Domain.Interfaces;
using DnDSheetApi.Domain.Models;
using DnDSheetApi.Infrastructure.Data;
using System.Security.Claims;

namespace DnDSheetApi.Infrastructure.Repositories;

public class SheetRepository : ISheetRepository
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SheetRepository(AppDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    private int CurrentUserId => int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");

    private IQueryable<CharacterSheet> OwnedSheets => _context.CharacterSheets.Where(s => s.OwnerId == CurrentUserId);

    public async Task<IEnumerable<SheetSummary>> GetAllAsync()
    {
        return await OwnedSheets
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SheetSummary(s.Id, s.CharacterName, s.CreatedAt, s.UpdatedAt))
            .ToListAsync();
    }

    public async Task<SheetDetails?> GetByIdAsync(int id)
    {
        return await _context.CharacterSheets
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SheetDetails(s.Id, s.CharacterName, s.SheetData, s.CreatedAt, s.UpdatedAt))
            .SingleOrDefaultAsync();
    }

    public async Task<string?> GetEditPasswordHashAsync(int id)
    {
        return await OwnedSheets
            .Where(s => s.Id == id)
            .Select(s => s.EditPasswordHash)
            .SingleOrDefaultAsync();
    }

    public async Task AddAsync(CharacterSheet sheet)
    {
        sheet.OwnerId = CurrentUserId;
        await _context.CharacterSheets.AddAsync(sheet);
    }

    public async Task<bool> UpdateDataAsync(int id, string expectedPasswordHash, string sheetData, DateTime updatedAt)
    {
        // Recheck the hash atomically: a concurrent password reset must revoke this write.
        return await OwnedSheets
            .Where(s => s.Id == id && s.EditPasswordHash == expectedPasswordHash)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.SheetData, sheetData)
                .SetProperty(s => s.UpdatedAt, updatedAt)) > 0;
    }

    public async Task<bool> DeleteAsync(int id, string expectedPasswordHash)
    {
        return await OwnedSheets
            .Where(s => s.Id == id && s.EditPasswordHash == expectedPasswordHash)
            .ExecuteDeleteAsync() > 0;
    }

    public async Task<bool> ResetPasswordAsync(int id, string passwordHash, DateTime updatedAt)
    {
        // Updating only these columns avoids fetching or rewriting the sheet/image.
        return await OwnedSheets
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.EditPasswordHash, passwordHash)
                .SetProperty(s => s.UpdatedAt, updatedAt)) > 0;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
