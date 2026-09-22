using AspNetProject.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AspNetProject.Users.Infrastructure.DataAccess;

public sealed class UsersDbContext(DbContextOptions<UsersDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.ToTable("users");
        user.HasKey(x => x.Id);
        user.Property(x => x.Id).ValueGeneratedNever();
        user.Property(x => x.Login).HasMaxLength(50).IsRequired();
        user.HasIndex(x => x.Login).IsUnique();
        user.Property(x => x.PasswordHash).IsRequired();
        user.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
    }
}
