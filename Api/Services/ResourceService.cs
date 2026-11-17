using Api.Contracts;
using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface IResourceService
{
    Task<IReadOnlyList<ResourceResponse>> GetAllAsync(ResourceType? type, bool includeInactive);
    Task<ResourceResponse> GetAsync(Guid id);
    Task<ResourceResponse> GetBySlugAsync(string slug);
    Task<AvailabilityResponse> GetAvailabilityAsync(Guid id, DateTimeOffset from, DateTimeOffset to, Guid currentUserId);
    Task<ResourceResponse> CreateAsync(SaveResourceRequest request);
    Task<ResourceResponse> UpdateAsync(Guid id, SaveResourceRequest request);
}

public sealed class ResourceService(AppDbContext db) : IResourceService
{
    public async Task<IReadOnlyList<ResourceResponse>> GetAllAsync(ResourceType? type, bool includeInactive)
    {
        var query = db.Resources.AsNoTracking().Include(r => r.Constraints).AsQueryable();
        if (!includeInactive) query = query.Where(r => r.IsActive);
        if (type is not null) query = query.Where(r => r.Type == type);
        // Explicit order: enums persist as strings, and alphabetical would put cars before rooms.
        var resources = await query
            .OrderBy(r => r.Type != ResourceType.Room)
            .ThenBy(r => r.Type != ResourceType.Car)
            .ThenBy(r => r.Name)
            .ToListAsync();
        return resources.Select(ToResponse).ToList();
    }

    public async Task<ResourceResponse> GetAsync(Guid id)
    {
        var resource = await db.Resources.AsNoTracking()
            .Include(r => r.Constraints)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException("Resource not found.");
        return ToResponse(resource);
    }

    public async Task<ResourceResponse> GetBySlugAsync(string slug)
    {
        var resource = await db.Resources.AsNoTracking()
            .Include(r => r.Constraints)
            .FirstOrDefaultAsync(r => r.Slug == slug)
            ?? throw new KeyNotFoundException("Resource not found.");
        return ToResponse(resource);
    }

    public async Task<AvailabilityResponse> GetAvailabilityAsync(Guid id, DateTimeOffset from, DateTimeOffset to, Guid currentUserId)
    {
        (from, to) = (from.ToUniversalTime(), to.ToUniversalTime());
        var resource = await db.Resources.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException("Resource not found.");

        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => b.ResourceId == id
                        && b.Status == BookingStatus.Confirmed
                        && b.Start < to && b.End > from)
            .OrderBy(b => b.Start)
            .Select(b => new AvailabilityBookingResponse(b.Id, b.Start, b.End, b.Quantity, b.UserId == currentUserId))
            .ToListAsync();

        return new AvailabilityResponse(id, resource.Capacity, bookings);
    }

    public async Task<ResourceResponse> CreateAsync(SaveResourceRequest request)
    {
        var resource = new Resource
        {
            Name = request.Name,
            Slug = await UniqueSlugAsync(Slugs.From(request.Name)),
            Type = request.Type,
            Capacity = request.Capacity,
            Seats = request.Seats,
            Location = request.Location,
            Description = request.Description,
            IsActive = request.IsActive,
            CreatedOn = DateTimeOffset.UtcNow,
            Constraints = request.Constraints
                .Select(c => new ResourceConstraint { Type = c.Type, ParamsJson = c.ParamsJson })
                .ToList(),
        };
        db.Resources.Add(resource);
        await db.SaveChangesAsync();
        return ToResponse(resource);
    }

    public async Task<ResourceResponse> UpdateAsync(Guid id, SaveResourceRequest request)
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
        // Capacity changes apply to new bookings only: existing rows keep their IsExclusive flag.
        resource.Constraints.Clear();
        foreach (var c in request.Constraints)
        {
            resource.Constraints.Add(new ResourceConstraint { Type = c.Type, ParamsJson = c.ParamsJson });
        }

        await db.SaveChangesAsync();
        return ToResponse(resource);
    }

    /// <summary>Two resources called "Huddle" become huddle and huddle-2. The unique index is the
    /// backstop; this just keeps a second resource with a familiar name from failing.</summary>
    private async Task<string> UniqueSlugAsync(string slug)
    {
        var taken = await db.Resources.AsNoTracking()
            .Where(r => r.Slug == slug || r.Slug.StartsWith(slug + "-"))
            .Select(r => r.Slug)
            .ToListAsync();

        var candidate = slug;
        var n = 1;
        while (taken.Contains(candidate, StringComparer.Ordinal))
            candidate = $"{slug}-{++n}";
        return candidate;
    }

    private static ResourceResponse ToResponse(Resource r) => new(
        r.Id, r.Name, r.Slug, r.Type, r.Capacity, r.Seats, r.Location, r.Description, r.IsActive, r.CreatedOn,
        r.Constraints.Select(c => new ConstraintResponse(c.Id, c.Type, c.ParamsJson)).ToList());
}
