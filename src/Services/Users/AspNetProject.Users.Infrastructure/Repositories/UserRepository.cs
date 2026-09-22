using AspNetProject.Users.Application.Interfaces;
using AspNetProject.Users.Domain.Entities;
using AspNetProject.Users.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace AspNetProject.Users.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly UsersDbContext _context;

    public UserRepository(UsersDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Login == login, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}