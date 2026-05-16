using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Dtos;
using Tracker.Entities;
using Tracker.Services;

namespace Tracker.Controllers;

[ApiController]
[Authorize]
[Route("api/batches")]
public class BatchesController : TenantControllerBase
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pending", "InProcess", "Completed", "Hold", "Rejected", "Delivered"
    };

    private readonly AppDbContext _db;
    private readonly INumberGenerator _ng;
    public BatchesController(AppDbContext db, INumberGenerator ng) { _db = db; _ng = ng; }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (Guid?)null;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BatchDto>>> List(
        [FromQuery] Guid? shopfloorId, [FromQuery] bool includeClosed, CancellationToken ct)
    {
        var q = _db.Batches.AsNoTracking().Where(b => b.TenantId == TenantId);
        if (shopfloorId.HasValue) q = q.Where(b => b.CurrentShopfloorId == shopfloorId.Value);
        if (!includeClosed) q = q.Where(b => b.ClosedAtUtc == null);

        var items = await q
            .OrderByDescending(b => b.LastMovedAtUtc)
            .Select(b => new BatchDto(
                b.Id, b.Number, b.BatchNo,
                b.CurrentShopfloorId, b.CurrentShopfloor.Code, b.CurrentShopfloor.Name,
                b.Status, b.Remarks,
                b.Sheets.Count,
                b.CreatedAtUtc, b.LastMovedAtUtc, b.ClosedAtUtc,
                b.Sheets.OrderBy(s => s.SheetNo).Select(s => new BatchSheetSummary(
                    s.Id, s.SheetNo, s.Customer != null ? s.Customer.Name : null, s.Status)).ToList()))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BatchDto>> Get(Guid id, CancellationToken ct) => await ToDto(id, ct);

    [HttpPost]
    public async Task<ActionResult<BatchDto>> Create(BatchCreateRequest req, CancellationToken ct)
    {
        if (req.SheetIds.Count == 0)
            return BadRequest(new { error = "Select at least one sheet to batch." });

        var floor = await _db.Shopfloors.FirstOrDefaultAsync(s => s.Id == req.ShopfloorId && s.TenantId == TenantId, ct);
        if (floor is null) return BadRequest(new { error = "Shopfloor not found." });
        if (string.Equals(floor.BatchMode, "None", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "This shopfloor doesn't support batches." });

        var sheets = await _db.GlassSheets
            .Where(g => g.TenantId == TenantId && req.SheetIds.Contains(g.Id))
            .ToListAsync(ct);
        if (sheets.Count != req.SheetIds.Count)
            return BadRequest(new { error = "Some selected sheets were not found." });
        if (sheets.Any(s => s.CurrentShopfloorId != floor.Id))
            return BadRequest(new { error = "All sheets must already be on this shopfloor." });
        if (sheets.Any(s => s.BatchId.HasValue))
            return BadRequest(new { error = "Some sheets are already in a batch. Dissolve those batches first." });

        var now = DateTime.UtcNow;
        var batch = new Batch
        {
            TenantId = TenantId,
            Number = await _ng.NextBatchAsync(TenantId, ct),
            BatchNo = await NextBatchNoAsync(ct),
            CurrentShopfloorId = floor.Id,
            Status = "InProcess",
            Remarks = req.Remarks,
            CreatedAtUtc = now,
            LastMovedAtUtc = now
        };
        _db.Batches.Add(batch);
        foreach (var s in sheets)
        {
            s.Batch = batch;
            s.LastMovedAtUtc = now;
        }
        await _db.SaveChangesAsync(ct);
        return await ToDto(batch.Id, ct);
    }

    [HttpPost("move")]
    public async Task<ActionResult<int>> Move(BatchMoveRequest req, CancellationToken ct)
    {
        if (req.BatchIds.Count == 0) return Ok(0);
        var target = await _db.Shopfloors.FirstOrDefaultAsync(s => s.Id == req.ToShopfloorId && s.TenantId == TenantId, ct);
        if (target is null) return BadRequest(new { error = "Target shopfloor not found." });
        if (!target.IsActive) return BadRequest(new { error = "Target shopfloor is inactive." });

        var batches = await _db.Batches
            .Include(b => b.Sheets)
            .Where(b => b.TenantId == TenantId && req.BatchIds.Contains(b.Id))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var newStatus = target.IsStorage ? "Pending" : "InProcess";
        var batchModeOnTarget = !string.Equals(target.BatchMode, "None", StringComparison.OrdinalIgnoreCase);

        foreach (var b in batches)
        {
            // Move every sheet in the batch
            foreach (var s in b.Sheets)
            {
                _db.SheetMovements.Add(new SheetMovement
                {
                    TenantId = TenantId,
                    GlassSheetId = s.Id,
                    FromShopfloorId = s.CurrentShopfloorId,
                    ToShopfloorId = target.Id,
                    MovedByUserId = CurrentUserId,
                    Remarks = req.Remarks ?? $"Batch {b.BatchNo}",
                    Status = newStatus,
                    MovedAtUtc = now
                });
                s.CurrentShopfloorId = target.Id;
                s.LastMovedAtUtc = now;
                s.Status = newStatus;
            }

            if (batchModeOnTarget)
            {
                // Batch survives — just relocate.
                b.CurrentShopfloorId = target.Id;
                b.LastMovedAtUtc = now;
            }
            else
            {
                // Destination doesn't support batches — dissolve.
                foreach (var s in b.Sheets) s.BatchId = null;
                b.ClosedAtUtc = now;
            }
        }

        await _db.SaveChangesAsync(ct);
        return Ok(batches.Count);
    }

    [HttpPost("status")]
    public async Task<ActionResult<int>> SetStatus(BatchStatusRequest req, CancellationToken ct)
    {
        if (req.BatchIds.Count == 0) return Ok(0);
        if (!AllowedStatuses.Contains(req.Status))
            return BadRequest(new { error = $"Unknown status '{req.Status}'." });

        var batches = await _db.Batches.Include(b => b.Sheets)
            .Where(b => b.TenantId == TenantId && req.BatchIds.Contains(b.Id))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var b in batches)
        {
            b.Status = req.Status;
            b.LastMovedAtUtc = now;
            foreach (var s in b.Sheets)
            {
                if (string.Equals(s.Status, req.Status, StringComparison.OrdinalIgnoreCase)) continue;
                _db.SheetMovements.Add(new SheetMovement
                {
                    TenantId = TenantId,
                    GlassSheetId = s.Id,
                    FromShopfloorId = s.CurrentShopfloorId,
                    ToShopfloorId = s.CurrentShopfloorId,
                    MovedByUserId = CurrentUserId,
                    Remarks = req.Remarks ?? $"Batch {b.BatchNo} status",
                    Status = req.Status,
                    MovedAtUtc = now
                });
                s.Status = req.Status;
                s.LastMovedAtUtc = now;
            }
        }
        await _db.SaveChangesAsync(ct);
        return Ok(batches.Count);
    }

    [HttpPost("{id:guid}/dissolve")]
    public async Task<IActionResult> Dissolve(Guid id, CancellationToken ct)
    {
        var batch = await _db.Batches.Include(b => b.Sheets)
            .FirstOrDefaultAsync(b => b.TenantId == TenantId && b.Id == id, ct);
        if (batch is null) return NotFound();

        var now = DateTime.UtcNow;
        foreach (var s in batch.Sheets) s.BatchId = null;
        batch.ClosedAtUtc = now;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ActionResult<BatchDto>> ToDto(Guid id, CancellationToken ct)
    {
        var dto = await _db.Batches.AsNoTracking()
            .Where(b => b.Id == id && b.TenantId == TenantId)
            .Select(b => new BatchDto(
                b.Id, b.Number, b.BatchNo,
                b.CurrentShopfloorId, b.CurrentShopfloor.Code, b.CurrentShopfloor.Name,
                b.Status, b.Remarks,
                b.Sheets.Count,
                b.CreatedAtUtc, b.LastMovedAtUtc, b.ClosedAtUtc,
                b.Sheets.OrderBy(s => s.SheetNo).Select(s => new BatchSheetSummary(
                    s.Id, s.SheetNo, s.Customer != null ? s.Customer.Name : null, s.Status)).ToList()))
            .FirstOrDefaultAsync(ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    private async Task<string> NextBatchNoAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow.ToString("yyMMdd");
        var prefix = $"B-{today}-";
        var existing = await _db.Batches
            .Where(b => b.TenantId == TenantId && b.BatchNo.StartsWith(prefix))
            .Select(b => b.BatchNo)
            .ToListAsync(ct);
        var next = existing.Count == 0
            ? 1
            : existing.Select(n => int.TryParse(n[(prefix.Length)..], out var v) ? v : 0).Max() + 1;
        return $"{prefix}{next:D3}";
    }
}
