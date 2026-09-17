using AspNetProject.Application.Dtos;
using AspNetProject.Domain.Entities;

namespace AspNetProject.Application.Interfaces;

public interface IUserService
{
    Task RegisterAsync(RegisterUserDto dto, CancellationToken cancellationToken = default);
    Task<string> LoginAsync(LoginUserDto dto, CancellationToken cancellationToken = default);
}