using System.ComponentModel.DataAnnotations;

namespace Tracker.Dtos;

public record GlassSheetDto(
    Guid Id,
    int Number,
    string SheetNo,
    string? OrderNo,
    Guid? CustomerId,
    string? CustomerName,
    string? GlassType,
    decimal? Thickness,
    decimal? Width,
    decimal? Height,
    int Quantity,
    string Status,
    Guid CurrentShopfloorId,
    string CurrentShopfloorCode,
    string CurrentShopfloorName,
    Guid? BatchId,
    string? BatchNo,
    string? Remarks,
    DateTime EntryAtUtc,
    DateTime LastMovedAtUtc);

public record SheetCreateRequest(
    [Required, MaxLength(60)] string SheetNo,
    [MaxLength(80)] string? OrderNo,
    Guid? CustomerId,
    [MaxLength(60)] string? GlassType,
    decimal? Thickness,
    decimal? Width,
    decimal? Height,
    int Quantity = 1,
    [MaxLength(250)] string? Remarks = null);

public record SheetBulkCreateRequest(IReadOnlyList<SheetCreateRequest> Sheets);

public record SheetBulkCreateResponse(int Created, int Skipped, IReadOnlyList<string> SkippedSheetNos);

public record SheetMoveRequest(
    [Required] IReadOnlyList<Guid> SheetIds,
    [Required] Guid ToShopfloorId,
    [MaxLength(250)] string? Remarks,
    bool CreateBatch = false);

public record BatchSheetSummary(
    Guid Id, string SheetNo, string? CustomerName, string Status);

public record BatchDto(
    Guid Id,
    int Number,
    string BatchNo,
    Guid CurrentShopfloorId,
    string CurrentShopfloorCode,
    string CurrentShopfloorName,
    string Status,
    string? Remarks,
    int SheetCount,
    DateTime CreatedAtUtc,
    DateTime LastMovedAtUtc,
    DateTime? ClosedAtUtc,
    IReadOnlyList<BatchSheetSummary> Sheets);

public record BatchCreateRequest(
    [Required] Guid ShopfloorId,
    [Required] IReadOnlyList<Guid> SheetIds,
    [MaxLength(250)] string? Remarks);

public record BatchMoveRequest(
    [Required] IReadOnlyList<Guid> BatchIds,
    [Required] Guid ToShopfloorId,
    [MaxLength(250)] string? Remarks);

public record BatchStatusRequest(
    [Required] IReadOnlyList<Guid> BatchIds,
    [Required, MaxLength(30)] string Status,
    [MaxLength(250)] string? Remarks);

public record SheetStatusRequest(
    [Required] IReadOnlyList<Guid> SheetIds,
    [Required, MaxLength(30)] string Status,
    [MaxLength(250)] string? Remarks);

public record SheetMovementDto(
    Guid Id,
    Guid GlassSheetId,
    Guid? FromShopfloorId,
    string? FromShopfloorName,
    Guid ToShopfloorId,
    string ToShopfloorName,
    string? MovedByEmail,
    string? Remarks,
    string? Status,
    DateTime MovedAtUtc);

public record DashboardStatsDto(
    int Total,
    int Active,                                    // everything except Delivered
    IReadOnlyDictionary<string, int> ByStatus,     // Pending / InProcess / Completed / Hold / Rejected / Delivered
    IReadOnlyList<DashboardFloorDto> ByShopfloor,
    int MovementsToday,
    int SheetsAddedToday);

public record DashboardFloorDto(
    Guid Id, string Code, string Name, int SequenceNo, bool IsStorage, int Count);
