using BCrypt.Net;
using DnDSheetApi.Application.Interfaces;
using DnDSheetApi.Domain.Entities;
using DnDSheetApi.Domain.Interfaces;

namespace DnDSheetApi.Application.Services;

public class SheetService : ISheetService
{
    private readonly ISheetRepository _sheetRepository;

    public SheetService(ISheetRepository sheetRepository)
    {
        _sheetRepository = sheetRepository;
    }

    public async Task<IEnumerable<CharacterSheet>> GetAllSheetsAsync()
    {
        return await _sheetRepository.GetAllAsync();
    }

    public async Task<CharacterSheet?> GetSheetByIdAsync(int id)
    {
        return await _sheetRepository.GetByIdAsync(id);
    }

    public async Task<CharacterSheet> CreateSheetAsync(string characterName, string editPassword)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(editPassword);
        
        var sheet = new CharacterSheet
        {
            CharacterName = characterName,
            EditPasswordHash = passwordHash,
            SheetData = "{}" // Default empty JSON object
        };

        await _sheetRepository.AddAsync(sheet);
        await _sheetRepository.SaveChangesAsync();

        return sheet;
    }

    public async Task<bool> UpdateSheetAsync(int id, string editPassword, string sheetData)
    {
        var sheet = await _sheetRepository.GetByIdAsync(id);
        if (sheet == null || !BCrypt.Net.BCrypt.Verify(editPassword, sheet.EditPasswordHash))
        {
            return false;
        }

        sheet.SheetData = sheetData;
        sheet.UpdatedAt = DateTime.UtcNow;

        _sheetRepository.Update(sheet);
        await _sheetRepository.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteSheetAsync(int id, string editPassword)
    {
        var sheet = await _sheetRepository.GetByIdAsync(id);
        if (sheet == null || !BCrypt.Net.BCrypt.Verify(editPassword, sheet.EditPasswordHash))
        {
            return false;
        }

        _sheetRepository.Remove(sheet);
        await _sheetRepository.SaveChangesAsync();

        return true;
    }

    public async Task<bool> VerifyPasswordAsync(int id, string editPassword)
    {
        var sheet = await _sheetRepository.GetByIdAsync(id);
        if (sheet == null)
        {
            return false;
        }

        return BCrypt.Net.BCrypt.Verify(editPassword, sheet.EditPasswordHash);
    }
}
