using System.Text.Json.Serialization;
using Api.Domain;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Api.Contracts;

// ── Request models — the frozen API contract, input half. Mirrored by
// client/src/app/core/models.ts; changing anything here requires updating
// docs/contracts.md and the TS mirror in the same commit.
// Every value-type field is required: a missing one is a 400, never a silent default —
// an omitted isActive would otherwise disable the user or resource being saved. ──

public record LoginRequest([property: JsonRequired] Guid UserId);

public record SaveResourceRequest(
    string Name,
    [property: JsonRequired] ResourceType Type,
    [property: JsonRequired] int Capacity,
    int? Seats,
    string? Location,
    string? Description,
    [property: JsonRequired] bool IsActive,
    IReadOnlyList<SaveConstraintRequest> Constraints);

public record SaveConstraintRequest([property: JsonRequired] ConstraintType Type, string ParamsJson);

public record SaveUserRequest(
    string Name,
    string Email,
    [property: JsonRequired] UserRole Role,
    [property: JsonRequired] bool IsActive);

public record CreateBookingRequest(
    [property: JsonRequired] Guid ResourceId,
    [property: JsonRequired] DateTimeOffset Start,
    [property: JsonRequired] DateTimeOffset End,
    [property: JsonRequired] int Quantity,
    string? Notes);

public record AssistantParseRequest(string Text);

/// <summary>A half-open [From, To) window from the query string — availability and the dashboard.</summary>
public record DateRangeQuery
{
    [BindRequired] public required DateTimeOffset From { get; init; }
    [BindRequired] public required DateTimeOffset To { get; init; }
}
