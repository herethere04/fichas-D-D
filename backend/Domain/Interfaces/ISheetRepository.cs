using DnDSheetApi.Domain.Entities;

namespace DnDSheetApi.Domain.Interfaces;

public interface ISheetRepository
{
    Task<IEnumerable<CharacterSheet>> GetAllAsync();
    Task<CharacterSheet?> GetByIdAsync(int id);
    Task AddAsync(CharacterSheet sheet);
    void Update(CharacterSheet sheet);
    void Remove(CharacterSheet sheet);
    Task SaveChangesAsync();
}
