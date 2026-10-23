using Api.Contracts;
using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface IResourceService
{
    Task<IReadOnlyList<ResourceDto>> GetAllAsync(ResourceType? type, bool includeInactive);
    Task<ResourceDto> GetAsync(Guid id);
    Task<AvailabilityDto> GetAvailabilityAsync(Guid id, DateTimeOffset from, DateTimeOffset to, Guid currentUserId);
    Task<ResourceDto> CreateAsync(SaveResourceRequest request);
    Task<ResourceDto> UpdateAsync(Guid id, SaveResourceRequest request);
}

public class ResourceService(AppDbContext db) : IResourceService
{
    public async Task<IReadOnlyList<ResourceDto>> GetAllAsync(ResourceType? type, bool includeInactive)
    {
        var query = db.Resources.AsNoTracking().Include(r => r.Constraints).AsQueryable();
        if (!includeInactive) query = query.Where(r => r.IsActive);
        if (type is not null) query = query.Where(r => r.Type == type);
        var resources = await query.OrderBy(r => r.Type).ThenBy(r => r.Name).ToListAsync();
        return resources.Select(ToDto).ToList();
    }

    public async Task<ResourceDto> GetAsync(Guid id)
    {
        var resource = await db.Resources.AsNoTracking()
            .Include(r => r.Constraints)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException("Resource not found.");
        return ToDto(resource);
    }

    public async Task<AvailabilityDto> GetAvailabilityAsync(Guid id, DateTimeOffset from, DateTimeOffset to, Guid currentUserId)
    {
        var resource = await db.Resources.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException("Resource not found.");

        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => b.ResourceId == id
                        && b.Status == BookingStatus.Confirmed
                        && b.Start < to && b.End > from)
            .OrderBy(b => b.Start)
            .Select(b => new AvailabilityBookingDto(b.Id, b.Start, b.End, b.Quantity, b.UserId == currentUserId))
            .ToListAsync();

        return new AvailabilityDto(id, resource.Capacity, bookings);
    }

    public async Task<ResourceDto> CreateAsync(SaveResourceRequest request)
    {
        var resource = new Resource
        {
            Name = request.Name,
            Type = request.Type,
            Capacity = request.Capacity,
            Seats = request.Seats,
            Location = request.Location,
            Description = request.Description,
            IsActive = request.IsActive,
            Constraints = request.Constraints
                .Select(c => new ResourceConstraint { Type = c.Type, ParamsJson = c.ParamsJson })
                .ToList(),
        };
        db.Resources.Add(resource);
        await db.SaveChangesAsync();
        return ToDto(resource);
    }

    public async Task<ResourceDto> UpdateAsync(Guid id, SaveResourceRequest request)
    {
        var resource = await db.Resources
            .Include(r => r.Constraints)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException("Resource not found.");

        resource.Name = request.Name;
        resource.Type = request.Type;
        resource.Capacity = request.Capacity;
        resource.Seats = request.Seats;
        resource.Location = request.Location;
        resource.Description = request.Description;
        resource.IsActive = request.IsActive;

        // Constraints are replaced wholesale — the admin UI always sends the full set.
        resource.Constraints.Clear();
        foreach (var c in request.Constraints)
        {
            resource.Constraints.Add(new ResourceConstraint { Type = c.Type, ParamsJson = c.ParamsJson });
        }

        await db.SaveChangesAsync();
        return ToDto(resource);
    }

    private static ResourceDto ToDto(Resource r) => new(
        r.Id, r.Name, r.Type, r.Capacity, r.Seats, r.Location, r.Description, r.IsActive,
        r.Constraints.Select(c => new ConstraintDto(c.Id, c.Type, c.ParamsJson)).ToList());
}
