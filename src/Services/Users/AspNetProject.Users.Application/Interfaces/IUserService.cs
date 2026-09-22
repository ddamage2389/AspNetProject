using AspNetProject.Users.Application.Dtos;
using AspNetProject.Users.Domain.Entities;

namespace AspNetProject.Users.Application.Interfaces;

public interface IUserService
{
    Task RegisterAsync(RegisterUserDto dto, CancellationToken cancellationToken = default);
    Task<string> LoginAsync(LoginUserDto dto, CancellationToken cancellationToken = default);
}