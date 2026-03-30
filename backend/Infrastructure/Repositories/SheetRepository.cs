using Microsoft.EntityFrameworkCore;
using DnDSheetApi.Domain.Entities;
using DnDSheetApi.Domain.Interfaces;
using DnDSheetApi.Infrastructure.Data;

namespace DnDSheetApi.Infrastructure.Repositories;

public class SheetRepository : ISheetRepository
{
    private readonly AppDbContext _context;

    public SheetRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<CharacterSheet>> GetAllAsync()
    {
        return await _context.CharacterSheets.OrderByDescending(s => s.CreatedAt).ToListAsync();
    }

    public async Task<CharacterSheet?> GetByIdAsync(int id)
    {
        return await _context.CharacterSheets.FindAsync(id);
    }

    public async Task AddAsync(CharacterSheet sheet)
    {
        await _context.CharacterSheets.AddAsync(sheet);
    }

    public void Update(CharacterSheet sheet)
    {
        _context.CharacterSheets.Update(sheet);
    }

    public void Remove(CharacterSheet sheet)
    {
        _context.CharacterSheets.Remove(sheet);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
