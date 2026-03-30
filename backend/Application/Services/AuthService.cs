using BCrypt.Net;
using DnDSheetApi.Application.Interfaces;
using DnDSheetApi.Domain.Interfaces;
using DnDSheetApi.Infrastructure.Security;

namespace DnDSheetApi.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;

    public AuthService(IUserRepository userRepository, ITokenService tokenService)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
    }

    public async Task<string?> AuthenticateAsync(string username, string password)
    {
        var user = await _userRepository.GetByUsernameAsync(username);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null;
        }

        return _tokenService.GenerateToken(user);
    }
}
