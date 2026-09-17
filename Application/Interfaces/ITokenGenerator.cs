using AspNetProject.Domain.Entities;

namespace AspNetProject.Application.Interfaces;

public interface ITokenGenerator
{
    string Generate(User user);
}