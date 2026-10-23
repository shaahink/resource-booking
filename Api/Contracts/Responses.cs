using Api.Domain;

namespace Api.Contracts;

// ── Response models — the frozen API contract, output half. Mirrored by
// client/src/app/core/models.ts; changing anything here requires updating
// docs/contracts.md and the TS mirror in the same commit. ──

public record UserResponse(Guid Id, string Name, string Email, UserRole Role, bool IsActive, DateTimeOffset CreatedOn);

public record ResourceResponse(
    Guid Id,
    string Name,
    ResourceType Type,
    int Capacity,
    int? Seats,
    string? Location,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedOn,
    IReadOnlyList<ConstraintResponse> Constraints);

public record ConstraintResponse(Guid Id, ConstraintType Type, string ParamsJson);

public record BookingResponse(
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
    DateTimeOffset CreatedOn);

/// <summary>Raw bookings in range; the client computes the grid. "Mine" lets the grid highlight the caller's bookings.</summary>
public record AvailabilityResponse(Guid ResourceId, int Capacity, IReadOnlyList<AvailabilityBookingResponse> Bookings);

public record AvailabilityBookingResponse(Guid Id, DateTimeOffset Start, DateTimeOffset End, int Quantity, bool Mine);

public record NotificationResponse(Guid Id, string Type, string Message, Guid? BookingId, DateTimeOffset CreatedOn, DateTimeOffset? ReadAt);

// ── Admin stats ──

public record DashboardResponse(
    IReadOnlyList<ResourceUtilisationResponse> Utilisation,
    IReadOnlyList<PeakHourResponse> PeakHours,
    IReadOnlyList<TopUserResponse> TopUsers,
    int BookingsInRange,
    double OverallUtilisationPct);

public record ResourceUtilisationResponse(
    Guid ResourceId, string Name, ResourceType Type,
    double BookedHours, double AvailableHours, double UtilisationPct, int BookingsCount);

public record PeakHourResponse(int Hour, int Count);

public record TopUserResponse(Guid UserId, string Name, int Count);

// ── AI ──

/// <summary>The model proposes; BookingRules disposes. Every proposal has already passed Validate.</summary>
public record AssistantParseResponse(string Summary, IReadOnlyList<AssistantProposalResponse> Proposals);

public record AssistantProposalResponse(Guid ResourceId, string ResourceName, DateTimeOffset Start, DateTimeOffset End, int Quantity);

/// <summary>Structured so the UI can lay it out: one headline, short highlights, one suggested action.</summary>
public record InsightsSummaryResponse(string Headline, IReadOnlyList<string> Highlights, string Suggestion, DateTimeOffset GeneratedAt);
