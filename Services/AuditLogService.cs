using Pressing.Data;
using Pressing.Models;

namespace Pressing.Services;

public sealed class AuditLogService(ApplicationDbContext database)
{
    public void Record(string userId, string action, string entity, object entityId, string? details = null)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("Un utilisateur est requis pour la trace d’audit.", nameof(userId));
        database.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = action, Entity = entity, EntityId = Convert.ToString(entityId) ?? string.Empty,
            Details = details, CreatedAtUtc = DateTime.UtcNow
        });
    }
}
