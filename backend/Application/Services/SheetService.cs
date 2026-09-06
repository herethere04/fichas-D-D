using System.Diagnostics;
using DnDSheetApi.Application.Interfaces;
using DnDSheetApi.Domain.Entities;
using DnDSheetApi.Domain.Interfaces;
using DnDSheetApi.Domain.Models;

namespace DnDSheetApi.Application.Services;

public class SheetService : ISheetService
{
    private readonly ISheetRepository _sheetRepository;
    private readonly ILogger<SheetService> _logger;

    public SheetService(ISheetRepository sheetRepository, ILogger<SheetService> logger)
    {
        _sheetRepository = sheetRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<SheetSummary>> GetAllSheetsAsync()
    {
        return await _sheetRepository.GetAllAsync();
    }

    public async Task<SheetDetails?> GetSheetByIdAsync(int id)
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
        var passwordHash = await _sheetRepository.GetEditPasswordHashAsync(id);
        if (passwordHash == null || !BCrypt.Net.BCrypt.Verify(editPassword, passwordHash))
        {
            return false;
        }

        return await _sheetRepository.UpdateDataAsync(id, passwordHash, sheetData, DateTime.UtcNow);
    }

    public async Task<bool> DeleteSheetAsync(int id, string editPassword)
    {
        var passwordHash = await _sheetRepository.GetEditPasswordHashAsync(id);
        if (passwordHash == null || !BCrypt.Net.BCrypt.Verify(editPassword, passwordHash))
        {
            return false;
        }

        return await _sheetRepository.DeleteAsync(id, passwordHash);
    }

    public async Task<bool> VerifyPasswordAsync(int id, string editPassword)
    {
        var started = Stopwatch.GetTimestamp();
        var passwordHash = await _sheetRepository.GetEditPasswordHashAsync(id);
        var databaseMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        var verificationStarted = Stopwatch.GetTimestamp();
        var isValid = passwordHash != null && BCrypt.Net.BCrypt.Verify(editPassword, passwordHash);
        var bcryptMs = Stopwatch.GetElapsedTime(verificationStarted).TotalMilliseconds;

        // Timings only: never log the supplied password, stored hash, token or sheet contents.
        _logger.LogInformation(
            "Edit password verification: database_ms={DatabaseMs:F1}, bcrypt_ms={BcryptMs:F1}, total_ms={TotalMs:F1}",
            databaseMs, bcryptMs, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        return isValid;
    }

    public async Task<bool> ResetPasswordDirectAsync(int id, string newPassword)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        return await _sheetRepository.ResetPasswordAsync(id, passwordHash, DateTime.UtcNow);
    }
}
