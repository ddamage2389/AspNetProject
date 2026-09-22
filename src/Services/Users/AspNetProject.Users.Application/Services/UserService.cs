using AspNetProject.Users.Application.Dtos;
using AspNetProject.Users.Application.Interfaces;
using AspNetProject.Users.Domain.Entities;

namespace AspNetProject.Users.Application.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;

    public UserService(IUserRepository userRepository, IPasswordHasher passwordHasher, ITokenGenerator tokenGenerator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task RegisterAsync(RegisterUserDto dto, CancellationToken cancellationToken = default)
    {
        var existing = await _userRepository.GetByLoginAsync(dto.Login.Trim(), cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException("Пользователь с таким логином уже существует.");
        }

        var passwordHash = _passwordHasher.Hash(dto.Password);
        var user = User.Create(dto.Login, passwordHash, Role.User);

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> LoginAsync(LoginUserDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByLoginAsync(dto.Login.Trim(), cancellationToken);

        if (user == null || !_passwordHasher.Verify(dto.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Неверный логин или пароль.");
        }

        return _tokenGenerator.Generate(user);
    }
}
