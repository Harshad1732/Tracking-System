using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tracker.Services;

namespace Tracker.Controllers;

public record SheetStatusDto(
    Guid Id, string Code, string Name, int SortOrder,
    bool IsInitial, bool IsTerminal, bool IsReplaceable,
    bool AppliesToSheets, bool AppliesToBatches,
    bool IsSystem, bool IsActive);

/// <summary>
/// Read-only catalog endpoint for the SheetStatus table. The frontend caches this once
/// per session and uses it to populate every status dropdown and to determine which
/// statuses allow replacement — same source of truth as the controllers' validation.
/// </summary>
[ApiController]
[Authorize]
[Route("api/sheet-statuses")]
public class SheetStatusesController : ControllerBase
{
    private readonly ISheetStatusService _statuses;
    public SheetStatusesController(ISheetStatusService statuses) => _statuses = statuses;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SheetStatusDto>>> List(CancellationToken ct)
    {
        var list = await _statuses.ListAsync(ct: ct);
        return Ok(list.Select(s => new SheetStatusDto(
            s.Id, s.Code, s.Name, s.SortOrder,
            s.IsInitial, s.IsTerminal, s.IsReplaceable,
            s.AppliesToSheets, s.AppliesToBatches,
            s.IsSystem, s.IsActive)).ToList());
    }
}
