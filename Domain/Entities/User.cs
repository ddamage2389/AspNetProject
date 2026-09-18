namespace AspNetProject.Domain.Entities;

public sealed class User
{
    private User() { }

    public Guid Id { get; private set; }
    public string Login { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public Role Role { get; private set; }

    public static User Create(string login, string passwordHash, Role role = Role.User)
    {
        if (string.IsNullOrWhiteSpace(login))
            throw new ArgumentException("Логин не может быть пустым", nameof(login));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Хеш пароля не может быть пустым", nameof(passwordHash));

        return new User
        {
            Id = Guid.NewGuid(),
            Login = login.Trim(),
            PasswordHash = passwordHash,
            Role = role
        };
    }
}