using System.ComponentModel.DataAnnotations;

namespace Tracker.Dtos;

public record PlanDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int MonthlyPriceCents,
    string Currency,
    int MaxSheets,
    int MaxUsers,
    int MaxShopfloors,
    int RetentionDays,
    int SortOrder,
    bool IsActive);

public record SubscriptionDto(
    Guid Id,
    PlanDto Plan,
    string Status,
    DateTime? TrialEndsAtUtc,
    DateTime? CurrentPeriodEndsAtUtc,
    DateTime? CanceledAtUtc,
    UsageDto Usage);

public record UsageDto(
    int SheetsUsed, int SheetsLimit,
    int UsersUsed, int UsersLimit,
    int ShopfloorsUsed, int ShopfloorsLimit);

public record UpgradePlanRequest([Required] string PlanCode);
