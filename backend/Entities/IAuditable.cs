namespace Tracker.Entities;

/// <summary>
/// Marker for master/reference entities that should track who created/modified the row
/// and when. AppDbContext.SaveChangesAsync auto-stamps these from the current HTTP user;
/// rows written during startup seeding leave the *By columns null (no HTTP context).
/// </summary>
public interface IAuditable
{
    Guid? CreatedBy { get; set; }
    DateTime CreatedAtUtc { get; set; }
    Guid? ModifiedBy { get; set; }
    DateTime? ModifiedAtUtc { get; set; }
}
