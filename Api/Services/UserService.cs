using Api.Contracts;
using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface IUserService
{
    Task<IReadOnlyList<UserResponse>> GetAllAsync();
    Task<UserResponse> CreateAsync(SaveUserRequest request);
    Task<UserResponse> UpdateAsync(Guid id, SaveUserRequest request);
}

public sealed class UserService(AppDbContext db) : IUserService
{
    public async Task<IReadOnlyList<UserResponse>> GetAllAsync() =>
        await db.Users.AsNoTracking()
            .OrderBy(u => u.Name)
            .Select(u => new UserResponse(u.Id, u.Name, u.Email, u.Role, u.IsActive, u.CreatedOn))
            .ToListAsync();

    public async Task<UserResponse> CreateAsync(SaveUserRequest request)
    {
        await EnsureEmailIsFreeAsync(request.Email, exceptId: Guid.Empty);

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            Role = request.Role,
            IsActive = request.IsActive,
            CreatedOn = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return ToResponse(user);
    }

    public async Task<UserResponse> UpdateAsync(Guid id, SaveUserRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new KeyNotFoundException("User not found.");
        await EnsureEmailIsFreeAsync(request.Email, exceptId: id);

        user.Name = request.Name;
        user.Email = request.Email;
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        await db.SaveChangesAsync();
        return ToResponse(user);
    }

    /// <summary>The DB has a unique index on Email as the backstop; this check turns the
    /// inevitable collision into a 409 with a message instead of a generic 500.</summary>
    private async Task EnsureEmailIsFreeAsync(string email, Guid exceptId)
    {
        var taken = await db.Users.AsNoTracking().AnyAsync(u => u.Email == email && u.Id != exceptId);
        if (taken) throw new ConflictException($"A user with email {email} already exists.");
    }

    private static UserResponse ToResponse(User u) =>
        new(u.Id, u.Name, u.Email, u.Role, u.IsActive, u.CreatedOn);
}
