using DnDSheetApi.Domain.Entities;
using DnDSheetApi.Domain.Models;

namespace DnDSheetApi.Domain.Interfaces;

public interface ISheetRepository
{
    Task<IEnumerable<SheetSummary>> GetAllAsync();
    Task<SheetDetails?> GetByIdAsync(int id);
    Task<string?> GetEditPasswordHashAsync(int id);
    Task AddAsync(CharacterSheet sheet);
    Task<bool> UpdateDataAsync(int id, string expectedPasswordHash, string sheetData, DateTime updatedAt);
    Task<bool> DeleteAsync(int id, string expectedPasswordHash);
    Task<bool> ResetPasswordAsync(int id, string passwordHash, DateTime updatedAt);
    Task SaveChangesAsync();
}
