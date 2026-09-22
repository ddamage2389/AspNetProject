using AspNetProject.Users.Domain.Entities;

namespace AspNetProject.Users.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}