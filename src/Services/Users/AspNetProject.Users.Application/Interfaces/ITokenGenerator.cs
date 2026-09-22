using AspNetProject.Users.Domain.Entities;

namespace AspNetProject.Users.Application.Interfaces;

public interface ITokenGenerator
{
    string Generate(User user);
}