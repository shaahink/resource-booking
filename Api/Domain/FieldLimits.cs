namespace Api.Domain;

/// <summary>Column lengths and value bounds, shared by the database mapping (AppDbContext) and the
/// request validators so the two can never disagree. Changing a length needs a migration.</summary>
public static class FieldLimits
{
    public const int Name = 120;
    public const int Slug = 140;
    public const int Email = 200;
    public const int Location = 200;
    public const int Description = 1000;
    public const int Notes = 500;
    public const int AssistantText = 500;
    public const int EnumName = 16;
    public const int TypeName = 40;
    public const int NotificationMessage = 500;

    public const int MaxQuantity = 1000;
    public const int MaxCapacity = 10_000;
}
