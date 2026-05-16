namespace Tracker.Dtos;

public record PlatformTenantDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    int UserCount,
    int ShopfloorCount,
    int SheetCount,
    string? PlanCode,
    string? SubscriptionStatus,
    DateTime CreatedAtUtc);
