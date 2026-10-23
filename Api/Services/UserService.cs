using Api.Contracts;
using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetAllAsync();
    Task<UserDto> CreateAsync(SaveUserRequest request);
    Task<UserDto> UpdateAsync(Guid id, SaveUserRequest request);
}

public class UserService(AppDbContext db) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> GetAllAsync() =>
        await db.Users.AsNoTracking()
            .OrderBy(u => u.Name)
            .Select(u => new UserDto(u.Id, u.Name, u.Email, u.Role))
            .ToListAsync();

    public async Task<UserDto> CreateAsync(SaveUserRequest request)
    {
        var user = new User { Name = request.Name, Email = request.Email, Role = request.Role };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new UserDto(user.Id, user.Name, user.Email, user.Role);
    }

    public async Task<UserDto> UpdateAsync(Guid id, SaveUserRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new KeyNotFoundException("User not found.");
        user.Name = request.Name;
        user.Email = request.Email;
        user.Role = request.Role;
        await db.SaveChangesAsync();
        return new UserDto(user.Id, user.Name, user.Email, user.Role);
    }
}
