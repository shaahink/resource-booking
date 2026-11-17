namespace Api.Domain;

/// <summary>
/// Everything the rules need, gathered by the service layer. The rules themselves are pure:
/// no EF, no clock, no I/O — fully unit-testable.
/// </summary>
public record BookingContext(
    Resource Resource,
    IReadOnlyList<ResourceConstraint> Constraints,
    User User,
    DateTimeOffset Start,
    DateTimeOffset End,
    int Quantity,
    IReadOnlyList<Booking> OverlappingConfirmedBookings,
    IReadOnlyList<Booking> UsersUpcomingConfirmedBookings,
    DateTimeOffset Now);
