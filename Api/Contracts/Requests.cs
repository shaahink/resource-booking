using Api.Domain;

namespace Api.Contracts;

// ── Request models — the frozen API contract, input half. Mirrored by
// client/src/app/core/models.ts; changing anything here requires updating
// docs/contracts.md and the TS mirror in the same commit. ──

public record LoginRequest(Guid UserId);

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

public record SaveUserRequest(string Name, string Email, UserRole Role, bool IsActive);

public record CreateBookingRequest(Guid ResourceId, DateTimeOffset Start, DateTimeOffset End, int Quantity, string? Notes);

public record AssistantParseRequest(string Text);

/// <summary>A half-open [From, To) window from the query string — availability and the dashboard.</summary>
public record DateRangeQuery(DateTimeOffset From, DateTimeOffset To);
