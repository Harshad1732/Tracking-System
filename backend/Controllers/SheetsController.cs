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
[Route("api/sheets")]
public class SheetsController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPlanLimitService _limits;
    private readonly INumberGenerator _ng;
    public SheetsController(AppDbContext db, IPlanLimitService limits, INumberGenerator ng)
    {
        _db = db;
        _limits = limits;
        _ng = ng;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (Guid?)null;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GlassSheetDto>>> List(
        [FromQuery] Guid? shopfloorId,
        [FromQuery] string? status,
        [FromQuery] Guid? customerId,
        [FromQuery] bool? isStorage,
        [FromQuery] bool? unbatched,
        CancellationToken ct)
    {
        var q = _db.GlassSheets.AsNoTracking().Where(g => g.TenantId == TenantId);
        if (shopfloorId.HasValue) q = q.Where(g => g.CurrentShopfloorId == shopfloorId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(g => g.Status == status);
        if (customerId.HasValue) q = q.Where(g => g.CustomerId == customerId.Value);
        if (isStorage.HasValue) q = q.Where(g => g.CurrentShopfloor.IsStorage == isStorage.Value);
        if (unbatched == true) q = q.Where(g => g.BatchId == null);

        var items = await q
            .OrderByDescending(g => g.LastMovedAtUtc)
            .Select(g => new GlassSheetDto(
                g.Id, g.Number, g.SheetNo, g.OrderNo,
                g.CustomerId, g.Customer != null ? g.Customer.Name : null,
                g.GlassType, g.Thickness, g.Width, g.Height, g.Quantity,
                g.Status,
                g.CurrentShopfloorId, g.CurrentShopfloor.Code, g.CurrentShopfloor.Name,
                g.BatchId, g.Batch != null ? g.Batch.BatchNo : null,
                g.Remarks, g.EntryAtUtc, g.LastMovedAtUtc))
            .ToListAsync(ct);
        return Ok(items);
    }

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pending", "InProcess", "Completed", "Hold", "Rejected", "Delivered"
    };

    [HttpPost("status")]
    public async Task<ActionResult<int>> SetStatus(SheetStatusRequest req, CancellationToken ct)
    {
        if (req.SheetIds.Count == 0) return Ok(0);
        if (!AllowedStatuses.Contains(req.Status))
            return BadRequest(new { error = $"Unknown status '{req.Status}'." });

        var sheets = await _db.GlassSheets
            .Where(g => g.TenantId == TenantId && req.SheetIds.Contains(g.Id))
            .ToListAsync(ct);
        if (sheets.Count == 0) return Ok(0);

        var now = DateTime.UtcNow;
        foreach (var s in sheets)
        {
            if (string.Equals(s.Status, req.Status, StringComparison.OrdinalIgnoreCase)) continue;
            _db.SheetMovements.Add(new SheetMovement
            {
                TenantId = TenantId,
                GlassSheetId = s.Id,
                FromShopfloorId = s.CurrentShopfloorId,
                ToShopfloorId = s.CurrentShopfloorId,
                MovedByUserId = CurrentUserId,
                Remarks = req.Remarks,
                Status = req.Status,
                MovedAtUtc = now
            });
            s.Status = req.Status;
            s.LastMovedAtUtc = now;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(sheets.Count);
    }

    [HttpPost]
    public async Task<ActionResult<GlassSheetDto>> Create(SheetCreateRequest req, CancellationToken ct)
    {
        var storage = await _db.Shopfloors
            .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.IsStorage && s.IsActive, ct);
        if (storage is null) return BadRequest(new { error = "No Storage shopfloor configured. Add one in the Shopfloor master." });

        var limit = await _limits.CheckSheetsAsync(TenantId, 1, ct);
        if (!limit.Allowed) return StatusCode(402, new { error = limit.ErrorMessage, limit = limit.Limit, current = limit.Current });

        if (await _db.GlassSheets.AnyAsync(g => g.TenantId == TenantId && g.SheetNo == req.SheetNo, ct))
            return Conflict(new { error = $"Sheet number {req.SheetNo} already exists." });
        if (req.CustomerId is { } cid && !await _db.Customers.AnyAsync(c => c.Id == cid && c.TenantId == TenantId, ct))
            return BadRequest(new { error = "Customer not found." });

        var sheet = new GlassSheet
        {
            TenantId = TenantId,
            Number = await _ng.NextSheetAsync(TenantId, ct),
            SheetNo = req.SheetNo,
            OrderNo = req.OrderNo,
            CustomerId = req.CustomerId,
            GlassType = req.GlassType,
            Thickness = req.Thickness,
            Width = req.Width,
            Height = req.Height,
            Quantity = req.Quantity,
            Status = "Pending",
            CurrentShopfloorId = storage.Id,
            Remarks = req.Remarks
        };
        _db.GlassSheets.Add(sheet);
        _db.SheetMovements.Add(new SheetMovement
        {
            TenantId = TenantId,
            GlassSheetId = sheet.Id,
            FromShopfloorId = null,
            ToShopfloorId = storage.Id,
            MovedByUserId = CurrentUserId,
            Remarks = "Created"
        });
        await _db.SaveChangesAsync(ct);
        return await ToDto(sheet.Id, ct);
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<SheetBulkCreateResponse>> BulkCreate(
        SheetBulkCreateRequest req, CancellationToken ct)
    {
        if (req.Sheets.Count == 0)
            return Ok(new SheetBulkCreateResponse(0, 0, Array.Empty<string>()));

        var storage = await _db.Shopfloors
            .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.IsStorage && s.IsActive, ct);
        if (storage is null) return BadRequest(new { error = "No Storage shopfloor configured." });

        var limit = await _limits.CheckSheetsAsync(TenantId, req.Sheets.Count, ct);
        if (!limit.Allowed) return StatusCode(402, new { error = limit.ErrorMessage, limit = limit.Limit, current = limit.Current });

        var existing = await _db.GlassSheets
            .Where(g => g.TenantId == TenantId)
            .Select(g => g.SheetNo)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var skipped = new List<string>();
        var toAdd = new List<GlassSheet>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Reserve a contiguous Number range up front so all bulk-imported sheets
        // get sequential numbers without one MAX(Number) call per row.
        var nextNumber = await _ng.NextSheetAsync(TenantId, ct);

        foreach (var s in req.Sheets)
        {
            if (string.IsNullOrWhiteSpace(s.SheetNo) ||
                existingSet.Contains(s.SheetNo) ||
                !seen.Add(s.SheetNo))
            {
                skipped.Add(s.SheetNo ?? "(blank)");
                continue;
            }
            toAdd.Add(new GlassSheet
            {
                TenantId = TenantId,
                Number = nextNumber++,
                SheetNo = s.SheetNo,
                OrderNo = s.OrderNo,
                CustomerId = s.CustomerId,
                GlassType = s.GlassType,
                Thickness = s.Thickness,
                Width = s.Width,
                Height = s.Height,
                Quantity = s.Quantity <= 0 ? 1 : s.Quantity,
                Status = "Pending",
                CurrentShopfloorId = storage.Id,
                Remarks = s.Remarks
            });
        }

        if (toAdd.Count == 0) return Ok(new SheetBulkCreateResponse(0, skipped.Count, skipped));

        _db.GlassSheets.AddRange(toAdd);
        var movements = toAdd.Select(sheet => new SheetMovement
        {
            TenantId = TenantId,
            GlassSheetId = sheet.Id,
            FromShopfloorId = null,
            ToShopfloorId = storage.Id,
            MovedByUserId = CurrentUserId,
            Remarks = "Imported"
        });
        _db.SheetMovements.AddRange(movements);
        await _db.SaveChangesAsync(ct);
        return Ok(new SheetBulkCreateResponse(toAdd.Count, skipped.Count, skipped));
    }

    [HttpPost("move")]
    public async Task<ActionResult<int>> Move(SheetMoveRequest req, CancellationToken ct)
    {
        if (req.SheetIds.Count == 0) return Ok(0);

        var target = await _db.Shopfloors
            .FirstOrDefaultAsync(s => s.Id == req.ToShopfloorId && s.TenantId == TenantId, ct);
        if (target is null) return BadRequest(new { error = "Target shopfloor not found." });
        if (!target.IsActive) return BadRequest(new { error = "Target shopfloor is inactive." });

        var sheets = await _db.GlassSheets
            .Where(g => g.TenantId == TenantId && req.SheetIds.Contains(g.Id))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var sourceBatchIds = sheets.Where(s => s.BatchId.HasValue && s.CurrentShopfloorId != target.Id)
            .Select(s => s.BatchId!.Value).Distinct().ToList();

        foreach (var s in sheets)
        {
            if (s.CurrentShopfloorId == target.Id) continue;
            var newStatus = target.IsStorage ? "Pending" : "InProcess";
            _db.SheetMovements.Add(new SheetMovement
            {
                TenantId = TenantId,
                GlassSheetId = s.Id,
                FromShopfloorId = s.CurrentShopfloorId,
                ToShopfloorId = target.Id,
                MovedByUserId = CurrentUserId,
                Remarks = req.Remarks,
                Status = newStatus,
                MovedAtUtc = now
            });
            s.CurrentShopfloorId = target.Id;
            s.LastMovedAtUtc = now;
            s.Status = newStatus;
            // Sheet leaves any current batch on move to a different floor.
            s.BatchId = null;
        }

        // Auto-create batch on the destination if it's AutoConfirm + user opted in.
        if (req.CreateBatch
            && string.Equals(target.BatchMode, "AutoConfirm", StringComparison.OrdinalIgnoreCase)
            && sheets.Count > 0)
        {
            var batch = new Batch
            {
                TenantId = TenantId,
                Number = await _ng.NextBatchAsync(TenantId, ct),
                BatchNo = await NextBatchNoAsync(ct),
                CurrentShopfloorId = target.Id,
                Status = "InProcess",
                Remarks = req.Remarks,
                CreatedAtUtc = now,
                LastMovedAtUtc = now
            };
            _db.Batches.Add(batch);
            foreach (var s in sheets) s.Batch = batch;
        }

        // Close source batches that no longer have any sheets (after this move).
        if (sourceBatchIds.Count > 0)
        {
            var stillUsed = await _db.GlassSheets
                .Where(g => g.BatchId.HasValue && sourceBatchIds.Contains(g.BatchId.Value))
                .Select(g => g.BatchId!.Value)
                .Distinct()
                .ToListAsync(ct);
            var orphans = sourceBatchIds.Except(stillUsed).ToList();
            if (orphans.Count > 0)
            {
                var batches = await _db.Batches
                    .Where(b => orphans.Contains(b.Id) && b.TenantId == TenantId)
                    .ToListAsync(ct);
                foreach (var b in batches) b.ClosedAtUtc = now;
            }
        }

        await _db.SaveChangesAsync(ct);
        return Ok(sheets.Count);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var sheet = await _db.GlassSheets.FirstOrDefaultAsync(g => g.Id == id && g.TenantId == TenantId, ct);
        if (sheet is null) return NotFound();
        _db.GlassSheets.Remove(sheet);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/movements")]
    public async Task<ActionResult<IReadOnlyList<SheetMovementDto>>> Movements(Guid id, CancellationToken ct)
    {
        var items = await _db.SheetMovements.AsNoTracking()
            .Where(m => m.TenantId == TenantId && m.GlassSheetId == id)
            .OrderByDescending(m => m.MovedAtUtc)
            .Select(m => new SheetMovementDto(
                m.Id, m.GlassSheetId,
                m.FromShopfloorId, m.FromShopfloor != null ? m.FromShopfloor.Name : null,
                m.ToShopfloorId, m.ToShopfloor.Name,
                m.MovedByUser != null ? m.MovedByUser.Email : null,
                m.Remarks, m.Status, m.MovedAtUtc))
            .ToListAsync(ct);
        return Ok(items);
    }

    private async Task<ActionResult<GlassSheetDto>> ToDto(Guid id, CancellationToken ct)
    {
        var dto = await _db.GlassSheets.AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new GlassSheetDto(
                g.Id, g.Number, g.SheetNo, g.OrderNo,
                g.CustomerId, g.Customer != null ? g.Customer.Name : null,
                g.GlassType, g.Thickness, g.Width, g.Height, g.Quantity,
                g.Status,
                g.CurrentShopfloorId, g.CurrentShopfloor.Code, g.CurrentShopfloor.Name,
                g.BatchId, g.Batch != null ? g.Batch.BatchNo : null,
                g.Remarks, g.EntryAtUtc, g.LastMovedAtUtc))
            .FirstAsync(ct);
        return Ok(dto);
    }

    private async Task<string> NextBatchNoAsync(CancellationToken ct)
    {
        // BatchNo format: B-YYMMDD-NNN (per-day sequence per tenant).
        var today = DateTime.UtcNow.ToString("yyMMdd");
        var prefix = $"B-{today}-";
        var lastSeq = await _db.Batches
            .Where(b => b.TenantId == TenantId && b.BatchNo.StartsWith(prefix))
            .Select(b => b.BatchNo)
            .ToListAsync(ct);
        var next = lastSeq.Count == 0
            ? 1
            : lastSeq.Select(n => int.TryParse(n[(prefix.Length)..], out var v) ? v : 0).Max() + 1;
        return $"{prefix}{next:D3}";
    }
}
