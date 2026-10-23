namespace Api.Services;

/// <summary>A uniqueness conflict outside the booking rules (e.g. duplicate email).
/// Mapped to 409 by ApiExceptionHandler.</summary>
public class ConflictException(string message) : Exception(message);
