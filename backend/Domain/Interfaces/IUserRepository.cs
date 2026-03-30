using DnDSheetApi.Domain.Entities;

namespace DnDSheetApi.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task AddAsync(User user);
    Task SaveChangesAsync();
    Task<bool> ExistsAsync(string username);
}
