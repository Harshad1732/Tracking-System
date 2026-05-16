using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Dtos;
using Tracker.Entities;
using Tracker.Filters;
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
    private readonly ISheetStatusService _statuses;
    public SheetsController(
        AppDbContext db, IPlanLimitService limits, INumberGenerator ng,
        ISheetStatusService statuses)
    {
        _db = db;
        _limits = limits;
        _ng = ng;
        _statuses = statuses;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (Guid?)null;

    [HttpGet]
    [RequirePermission(Resources.Sheets, Actions.View)]
    public async Task<ActionResult<IReadOnlyList<GlassSheetDto>>> List(
        [FromQuery] Guid? shopfloorId,
        [FromQuery] string? status,
        [FromQuery] Guid? customerId,
        [FromQuery] bool? isStorage,
        [FromQuery] bool? unbatched,
        CancellationToken ct)
    {
        var q = _db.GlassSheets.AsNoTracking().Where(g => g.TenantId == TenantId && g.PlantId == PlantId);
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
                g.Remarks, g.EntryAtUtc, g.LastMovedAtUtc,
                g.ReplacementForSheetId,
                g.ReplacementForSheet != null ? g.ReplacementForSheet.SheetNo : null,
                g.ReplacementReason))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("status")]
    [RequirePermission(Resources.Sheets, Actions.Edit)]
    public async Task<ActionResult<int>> SetStatus(SheetStatusRequest req, CancellationToken ct)
    {
        if (req.SheetIds.Count == 0) return Ok(0);
        if (!await _statuses.IsValidAsync(req.Status, forSheets: true, ct))
            return BadRequest(new { error = $"Unknown status '{req.Status}'." });

        var sheets = await _db.GlassSheets
            .Where(g => g.TenantId == TenantId && g.PlantId == PlantId && req.SheetIds.Contains(g.Id))
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
    [RequirePermission(Resources.Sheets, Actions.Add)]
    public async Task<ActionResult<GlassSheetDto>> Create(SheetCreateRequest req, CancellationToken ct)
    {
        // Storage is per-plant — find the storage floor for the CURRENT plant.
        var storage = await _db.Shopfloors
            .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.PlantId == PlantId && s.IsStorage && s.IsActive, ct);
        if (storage is null) return BadRequest(new { error = "No Storage shopfloor configured for this plant. Add one in the Shopfloor master." });

        var limit = await _limits.CheckSheetsAsync(TenantId, 1, ct);
        if (!limit.Allowed) return StatusCode(402, new { error = limit.ErrorMessage, limit = limit.Limit, current = limit.Current });

        if (await _db.GlassSheets.AnyAsync(g => g.TenantId == TenantId && g.SheetNo == req.SheetNo, ct))
            return Conflict(new { error = $"Sheet number {req.SheetNo} already exists." });
        if (req.CustomerId is { } cid && !await _db.Customers.AnyAsync(c => c.Id == cid && c.TenantId == TenantId, ct))
            return BadRequest(new { error = "Customer not found." });

        var initialStatus = await _statuses.InitialStatusCodeAsync(ct);
        var sheet = new GlassSheet
        {
            TenantId = TenantId,
            PlantId = PlantId,
            Number = await _ng.NextSheetAsync(TenantId, ct),
            SheetNo = req.SheetNo,
            OrderNo = req.OrderNo,
            CustomerId = req.CustomerId,
            GlassType = req.GlassType,
            Thickness = req.Thickness,
            Width = req.Width,
            Height = req.Height,
            Quantity = req.Quantity,
            Status = initialStatus,
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
    [RequirePermission(Resources.Sheets, Actions.Add)]
    public async Task<ActionResult<SheetBulkCreateResponse>> BulkCreate(
        SheetBulkCreateRequest req, CancellationToken ct)
    {
        if (req.Sheets.Count == 0)
            return Ok(new SheetBulkCreateResponse(0, 0, Array.Empty<string>()));

        var storage = await _db.Shopfloors
            .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.PlantId == PlantId && s.IsStorage && s.IsActive, ct);
        if (storage is null) return BadRequest(new { error = "No Storage shopfloor configured for this plant." });

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
        var initialStatus = await _statuses.InitialStatusCodeAsync(ct);

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
                PlantId = PlantId,
                Number = nextNumber++,
                SheetNo = s.SheetNo,
                OrderNo = s.OrderNo,
                CustomerId = s.CustomerId,
                GlassType = s.GlassType,
                Thickness = s.Thickness,
                Width = s.Width,
                Height = s.Height,
                Quantity = s.Quantity <= 0 ? 1 : s.Quantity,
                Status = initialStatus,
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
    [RequirePermission(Resources.Sheets, Actions.Edit)]
    public async Task<ActionResult<int>> Move(SheetMoveRequest req, CancellationToken ct)
    {
        if (req.SheetIds.Count == 0) return Ok(0);

        // Target floor must belong to the SAME plant. This is what stops a malicious
        // payload from moving sheets across plants.
        var target = await _db.Shopfloors
            .FirstOrDefaultAsync(s => s.Id == req.ToShopfloorId && s.TenantId == TenantId && s.PlantId == PlantId, ct);
        if (target is null) return BadRequest(new { error = "Target shopfloor not found in this plant." });
        if (!target.IsActive) return BadRequest(new { error = "Target shopfloor is inactive." });

        var sheets = await _db.GlassSheets
            .Where(g => g.TenantId == TenantId && g.PlantId == PlantId && req.SheetIds.Contains(g.Id))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var sourceBatchIds = sheets.Where(s => s.BatchId.HasValue && s.CurrentShopfloorId != target.Id)
            .Select(s => s.BatchId!.Value).Distinct().ToList();

        // Resolve the arrival status from the target floor — defaults seeded as
        // "Pending" for storage, "InProcess" for non-storage but each floor is editable.
        var arrivalStatus = target.ArrivalStatusCode
            ?? (target.IsStorage ? await _statuses.InitialStatusCodeAsync(ct) : "InProcess");

        foreach (var s in sheets)
        {
            if (s.CurrentShopfloorId == target.Id) continue;
            var newStatus = arrivalStatus;
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
                PlantId = PlantId,
                Number = await _ng.NextBatchAsync(TenantId, ct),
                BatchNo = await _ng.NextBatchNoAsync(TenantId, ct),
                CurrentShopfloorId = target.Id,
                Status = arrivalStatus,
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

    // Create a replacement sheet for a damaged/rejected/held one. Copies the original's
    // customer + glass details so the operator doesn't have to re-type everything, and
    // links the new sheet back to the original via ReplacementForSheetId so we can trace
    // "this order needed 2 attempts before it shipped." The original is left untouched —
    // change its status separately if you want it off the active board.
    [HttpPost("{id:guid}/replace")]
    [RequirePermission(Resources.Sheets, Actions.Add)]
    public async Task<ActionResult<GlassSheetDto>> Replace(Guid id, SheetReplaceRequest req, CancellationToken ct)
    {
        var original = await _db.GlassSheets
            .FirstOrDefaultAsync(g => g.Id == id && g.TenantId == TenantId && g.PlantId == PlantId, ct);
        if (original is null) return NotFound();

        var storage = await _db.Shopfloors
            .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.PlantId == PlantId && s.IsStorage && s.IsActive, ct);
        if (storage is null)
            return BadRequest(new { error = "No Storage shopfloor configured for this plant. Replacements always start in Storage." });

        var limit = await _limits.CheckSheetsAsync(TenantId, 1, ct);
        if (!limit.Allowed)
            return StatusCode(402, new { error = limit.ErrorMessage, limit = limit.Limit, current = limit.Current });

        // SheetNo: caller may supply one; otherwise derive a clearly-related identifier
        // by appending -R / -R2 / -R3 so it's obvious in lists which sheet replaced which.
        var newSheetNo = string.IsNullOrWhiteSpace(req.SheetNo)
            ? await DeriveReplacementSheetNoAsync(original.SheetNo, ct)
            : req.SheetNo.Trim();

        if (await _db.GlassSheets.AnyAsync(g => g.TenantId == TenantId && g.SheetNo == newSheetNo, ct))
            return Conflict(new { error = $"Sheet number {newSheetNo} already exists." });

        var replacement = new GlassSheet
        {
            TenantId = TenantId,
            PlantId = PlantId,
            Number = await _ng.NextSheetAsync(TenantId, ct),
            SheetNo = newSheetNo,
            OrderNo = original.OrderNo,
            CustomerId = original.CustomerId,
            GlassType = original.GlassType,
            Thickness = original.Thickness,
            Width = original.Width,
            Height = original.Height,
            Quantity = req.Quantity is int q && q > 0 ? q : original.Quantity,
            Status = await _statuses.InitialStatusCodeAsync(ct),
            CurrentShopfloorId = storage.Id,
            Remarks = $"Replacement for {original.SheetNo}",
            ReplacementForSheetId = original.Id,
            ReplacementReason = req.Reason
        };
        _db.GlassSheets.Add(replacement);
        _db.SheetMovements.Add(new SheetMovement
        {
            TenantId = TenantId,
            GlassSheetId = replacement.Id,
            FromShopfloorId = null,
            ToShopfloorId = storage.Id,
            MovedByUserId = CurrentUserId,
            Remarks = $"Replacement created: {req.Reason}"
        });

        // Annotate the original so its history reflects that a replacement was made.
        _db.SheetMovements.Add(new SheetMovement
        {
            TenantId = TenantId,
            GlassSheetId = original.Id,
            FromShopfloorId = original.CurrentShopfloorId,
            ToShopfloorId = original.CurrentShopfloorId,
            MovedByUserId = CurrentUserId,
            Remarks = $"Replacement issued as {newSheetNo}: {req.Reason}",
            Status = original.Status
        });

        await _db.SaveChangesAsync(ct);
        return await ToDto(replacement.Id, ct);
    }

    /// <summary>Max retry attempts when deriving a replacement sheet number.</summary>
    private const int MaxReplacementAttempts = 50;

    private async Task<string> DeriveReplacementSheetNoAsync(string original, CancellationToken ct)
    {
        // Strip any existing -R<n> suffix so a replacement of a replacement increments
        // cleanly (-R, -R2, -R3 …) rather than stacking suffixes.
        var match = System.Text.RegularExpressions.Regex.Match(original, "^(.*?)(-R(\\d*))?$");
        var stem = match.Success && match.Groups[1].Length > 0 ? match.Groups[1].Value : original;

        for (var attempt = 1; attempt <= MaxReplacementAttempts; attempt++)
        {
            var candidate = attempt == 1 ? $"{stem}-R" : $"{stem}-R{attempt}";
            if (!await _db.GlassSheets.AnyAsync(g => g.TenantId == TenantId && g.SheetNo == candidate, ct))
                return candidate;
        }
        // Fallback in the absurd case of N retries — embed a millisecond suffix.
        return $"{stem}-R{DateTime.UtcNow:HHmmssfff}";
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Resources.Sheets, Actions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var sheet = await _db.GlassSheets.FirstOrDefaultAsync(g => g.Id == id && g.TenantId == TenantId && g.PlantId == PlantId, ct);
        if (sheet is null) return NotFound();
        _db.GlassSheets.Remove(sheet);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/movements")]
    [RequirePermission(Resources.Sheets, Actions.View)]
    public async Task<ActionResult<IReadOnlyList<SheetMovementDto>>> Movements(Guid id, CancellationToken ct)
    {
        // Make sure the sheet itself is in the current plant before returning its history.
        var sheetExists = await _db.GlassSheets.AnyAsync(
            g => g.Id == id && g.TenantId == TenantId && g.PlantId == PlantId, ct);
        if (!sheetExists) return NotFound();

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
            .Where(g => g.Id == id && g.TenantId == TenantId && g.PlantId == PlantId)
            .Select(g => new GlassSheetDto(
                g.Id, g.Number, g.SheetNo, g.OrderNo,
                g.CustomerId, g.Customer != null ? g.Customer.Name : null,
                g.GlassType, g.Thickness, g.Width, g.Height, g.Quantity,
                g.Status,
                g.CurrentShopfloorId, g.CurrentShopfloor.Code, g.CurrentShopfloor.Name,
                g.BatchId, g.Batch != null ? g.Batch.BatchNo : null,
                g.Remarks, g.EntryAtUtc, g.LastMovedAtUtc,
                g.ReplacementForSheetId,
                g.ReplacementForSheet != null ? g.ReplacementForSheet.SheetNo : null,
                g.ReplacementReason))
            .FirstAsync(ct);
        return Ok(dto);
    }

}
