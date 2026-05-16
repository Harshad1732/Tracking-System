using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tracker.Data;
using Tracker.Dtos;
using Tracker.Filters;
using Tracker.Services;

namespace Tracker.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController : TenantControllerBase
{
    private readonly AppDbContext _db;
    public ReportsController(AppDbContext db) => _db = db;

    // Dashboard counters are visible to anyone with View permission — they're the home screen,
    // not a report. Full reports below require Perm.ViewReports.
    [HttpGet("dashboard-stats")]
    [RequirePermission(Resources.Sheets, Actions.View)]
    public async Task<ActionResult<DashboardStatsDto>> DashboardStats(CancellationToken ct)
    {
        var todayUtcStart = DateTime.UtcNow.Date;

        var sheets = await _db.GlassSheets.AsNoTracking()
            .Where(g => g.TenantId == TenantId && g.PlantId == PlantId)
            .Select(g => new { g.Status, g.EntryAtUtc })
            .ToListAsync(ct);

        var byStatus = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pending"] = 0, ["InProcess"] = 0, ["Completed"] = 0,
            ["Hold"] = 0,    ["Rejected"] = 0, ["Delivered"] = 0
        };
        foreach (var s in sheets)
        {
            if (byStatus.ContainsKey(s.Status)) byStatus[s.Status]++;
            else byStatus[s.Status] = 1;
        }

        var byFloor = await _db.Shopfloors.AsNoTracking()
            .Where(s => s.TenantId == TenantId && s.PlantId == PlantId && s.IsActive)
            .OrderBy(s => s.SequenceNo).ThenBy(s => s.Name)
            .Select(s => new DashboardFloorDto(
                s.Id, s.Code, s.Name, s.SequenceNo, s.IsStorage,
                _db.GlassSheets.Count(g => g.CurrentShopfloorId == s.Id)))
            .ToListAsync(ct);

        var movementsToday = await _db.SheetMovements.AsNoTracking()
            .CountAsync(m => m.TenantId == TenantId && m.GlassSheet.PlantId == PlantId && m.MovedAtUtc >= todayUtcStart, ct);
        var sheetsAddedToday = sheets.Count(s => s.EntryAtUtc >= todayUtcStart);

        return Ok(new DashboardStatsDto(
            Total: sheets.Count,
            Active: sheets.Count(s => !string.Equals(s.Status, "Delivered", StringComparison.OrdinalIgnoreCase)),
            ByStatus: byStatus,
            ByShopfloor: byFloor,
            MovementsToday: movementsToday,
            SheetsAddedToday: sheetsAddedToday));
    }

    [HttpGet("export/sheets.csv")]
    [RequirePermission(Resources.Reports, Actions.View)]
    public async Task<IActionResult> ExportSheets(
        [FromQuery] Guid? shopfloorId,
        [FromQuery] string? status,
        [FromQuery] string? excludeStatus,
        [FromQuery] Guid? customerId,
        [FromQuery] bool? isStorage,
        [FromQuery] string? fileName,
        CancellationToken ct)
    {
        var q = _db.GlassSheets.AsNoTracking().Where(g => g.TenantId == TenantId && g.PlantId == PlantId);
        if (shopfloorId.HasValue) q = q.Where(g => g.CurrentShopfloorId == shopfloorId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(g => g.Status == status);
        if (!string.IsNullOrWhiteSpace(excludeStatus)) q = q.Where(g => g.Status != excludeStatus);
        if (customerId.HasValue) q = q.Where(g => g.CustomerId == customerId.Value);
        if (isStorage.HasValue) q = q.Where(g => g.CurrentShopfloor.IsStorage == isStorage.Value);

        var rows = await q
            .OrderBy(g => g.CurrentShopfloor.SequenceNo).ThenByDescending(g => g.LastMovedAtUtc)
            .Select(g => new
            {
                g.SheetNo,
                g.OrderNo,
                CustomerName = g.Customer != null ? g.Customer.Name : null,
                FloorCode = g.CurrentShopfloor.Code,
                FloorName = g.CurrentShopfloor.Name,
                g.GlassType,
                g.Thickness,
                g.Width,
                g.Height,
                g.Quantity,
                g.Status,
                g.Remarks,
                g.EntryAtUtc,
                g.LastMovedAtUtc
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Sheet No,Order No,Customer,Floor Code,Floor Name,Glass Type,Thickness,Width,Height,Quantity,Status,Days,Entry Date,Last Moved,Remarks");
        var now = DateTime.UtcNow;
        foreach (var r in rows)
        {
            var days = (int)Math.Floor((now - r.EntryAtUtc).TotalDays);
            sb.AppendJoin(',', new[]
            {
                Csv(r.SheetNo),
                Csv(r.OrderNo),
                Csv(r.CustomerName),
                Csv(r.FloorCode),
                Csv(r.FloorName),
                Csv(r.GlassType),
                CsvNum(r.Thickness),
                CsvNum(r.Width),
                CsvNum(r.Height),
                r.Quantity.ToString(CultureInfo.InvariantCulture),
                Csv(r.Status),
                days.ToString(CultureInfo.InvariantCulture),
                CsvDate(r.EntryAtUtc),
                CsvDate(r.LastMovedAtUtc),
                Csv(r.Remarks)
            });
            sb.Append('\n');
        }
        return CsvFile(sb, fileName ?? "sheets");
    }

    [HttpGet("export/process.csv")]
    [RequirePermission(Resources.Reports, Actions.View)]
    public async Task<IActionResult> ExportProcess(CancellationToken ct)
    {
        var rows = await _db.Shopfloors.AsNoTracking()
            .Where(s => s.TenantId == TenantId && s.PlantId == PlantId)
            .OrderBy(s => s.SequenceNo).ThenBy(s => s.Name)
            .Select(s => new
            {
                s.Code,
                s.Name,
                s.SequenceNo,
                ProcessName = s.Process != null ? s.Process.Name : null,
                s.IsStorage,
                s.IsActive,
                SheetCount = _db.GlassSheets.Count(g => g.CurrentShopfloorId == s.Id)
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Code,Name,Sequence,Process,Type,Active,Sheet Count");
        foreach (var r in rows)
        {
            sb.AppendJoin(',', new[]
            {
                Csv(r.Code),
                Csv(r.Name),
                r.SequenceNo.ToString(CultureInfo.InvariantCulture),
                Csv(r.ProcessName),
                r.IsStorage ? "Storage" : "Shopfloor",
                r.IsActive ? "Yes" : "No",
                r.SheetCount.ToString(CultureInfo.InvariantCulture)
            });
            sb.Append('\n');
        }
        return CsvFile(sb, "shopfloor-counts");
    }

    private FileContentResult CsvFile(StringBuilder sb, string baseName)
    {
        // UTF-8 with BOM so Excel detects encoding properly
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[bom.Length + body.Length];
        Buffer.BlockCopy(bom, 0, bytes, 0, bom.Length);
        Buffer.BlockCopy(body, 0, bytes, bom.Length, body.Length);
        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd-HHmm", CultureInfo.InvariantCulture);
        var safe = new string(baseName.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return File(bytes, "text/csv; charset=utf-8", $"{safe}-{stamp}.csv");
    }

    private static string Csv(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        var needsQuote = v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        var escaped = v.Replace("\"", "\"\"");
        return needsQuote ? $"\"{escaped}\"" : escaped;
    }

    private static string CsvNum(decimal? v) =>
        v.HasValue ? v.Value.ToString(CultureInfo.InvariantCulture) : "";

    private static string CsvDate(DateTime v) =>
        v.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
