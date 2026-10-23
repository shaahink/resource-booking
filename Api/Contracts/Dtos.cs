using Api.Domain;

namespace Api.Contracts;

// ── The frozen API contract. Mirrored by client/src/app/core/models.ts. ──
// Changing anything here requires updating docs/contracts.md and the TS mirror in the same commit.

public record UserDto(Guid Id, string Name, string Email, UserRole Role);

public record LoginRequest(Guid UserId);

public record ResourceDto(
    Guid Id,
    string Name,
    ResourceType Type,
    int Capacity,
    int? Seats,
    string? Location,
    string? Description,
    bool IsActive,
    IReadOnlyList<ConstraintDto> Constraints);

public record ConstraintDto(Guid Id, ConstraintType Type, string ParamsJson);

public record SaveResourceRequest(
    string Name,
    ResourceType Type,
    int Capacity,
    int? Seats,
    string? Location,
    string? Description,
    bool IsActive,
    IReadOnlyList<SaveConstraintRequest> Constraints);

public record SaveConstraintRequest(ConstraintType Type, string ParamsJson);

public record SaveUserRequest(string Name, string Email, UserRole Role);

public record BookingDto(
    Guid Id,
    Guid ResourceId,
    string ResourceName,
    ResourceType ResourceType,
    Guid UserId,
    string UserName,
    DateTimeOffset Start,
    DateTimeOffset End,
    int Quantity,
    BookingStatus Status,
    string? Notes,
    DateTimeOffset CreatedAt);

public record CreateBookingRequest(Guid ResourceId, DateTimeOffset Start, DateTimeOffset End, int Quantity, string? Notes);

/// <summary>Raw bookings in range; the client computes the grid. "Mine" lets the grid highlight the caller's bookings.</summary>
public record AvailabilityDto(Guid ResourceId, int Capacity, IReadOnlyList<AvailabilityBookingDto> Bookings);

public record AvailabilityBookingDto(Guid Id, DateTimeOffset Start, DateTimeOffset End, int Quantity, bool Mine);

public record NotificationDto(Guid Id, string Type, string Message, Guid? BookingId, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

// ── Admin stats ──

public record DashboardDto(
    IReadOnlyList<ResourceUtilisationDto> Utilisation,
    IReadOnlyList<PeakHourDto> PeakHours,
    IReadOnlyList<TopUserDto> TopUsers,
    int BookingsThisWeek,
    double OverallUtilisationPct);

public record ResourceUtilisationDto(
    Guid ResourceId, string Name, ResourceType Type,
    double BookedHours, double AvailableHours, double UtilisationPct, int BookingsCount);

public record PeakHourDto(int Hour, int Count);

public record TopUserDto(Guid UserId, string Name, int Count);

// ── AI ──

public record AssistantParseRequest(string Text);

/// <summary>The model proposes; BookingRules disposes. Every proposal has already passed Validate.</summary>
public record AssistantParseResponse(string Summary, IReadOnlyList<AssistantProposalDto> Proposals);

public record AssistantProposalDto(Guid ResourceId, string ResourceName, DateTimeOffset Start, DateTimeOffset End, int Quantity);

public record InsightsSummaryDto(string Summary, DateTimeOffset GeneratedAt);
