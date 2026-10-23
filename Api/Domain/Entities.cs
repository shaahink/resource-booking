namespace Api.Domain;

public enum UserRole { User, Admin }

public enum ResourceType { Room, Car, Catering }

public enum BookingStatus { Confirmed, Cancelled }

public enum ConstraintType
{
    MaxDurationMinutes,
    MaxAdvanceDays,
    MinNoticeMinutes,
    OpeningHours,
    MaxConcurrentPerUser,
    MaxActiveBookingsPerUser,
}

public class User
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public UserRole Role { get; set; }
    public List<Booking> Bookings { get; set; } = [];
}

public class Resource
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public ResourceType Type { get; set; }

    /// <summary>Concurrent bookable units: 1 = exclusive (rooms, cars); N = pooled (catering portions).</summary>
    public int Capacity { get; set; } = 1;

    /// <summary>Room seats — metadata for search and the assistant, not a booking limit.</summary>
    public int? Seats { get; set; }

    public string? Location { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ResourceConstraint> Constraints { get; set; } = [];
    public List<Booking> Bookings { get; set; } = [];
}

public class ResourceConstraint
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public ConstraintType Type { get; set; }

    /// <summary>Typed parameters as JSON; shapes are documented in docs/contracts.md and parsed in BookingRules.</summary>
    public required string ParamsJson { get; set; }
}

public class Booking
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public int Quantity { get; set; } = 1;
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;

    /// <summary>Denormalised from Resource.Capacity == 1 at creation; drives the DB exclusion constraint.</summary>
    public bool IsExclusive { get; set; }

    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public Resource? Resource { get; set; }
    public User? User { get; set; }
}

public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? BookingId { get; set; }
    public required string Type { get; set; }
    public required string Message { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}
