using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Interfaces.Modules.AuditModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.AuditModule;

/// <summary>
/// Tracks an entry on the request's <see cref="AppDbContext"/> — the same instance every unit of
/// work of the request saves through — and leaves the saving to the handler.
/// </summary>
public sealed class KurinAuditWriter : IKurinAuditWriter
{
    private readonly AppDbContext _context;

    public KurinAuditWriter(AppDbContext context)
    {
        _context = context;
    }

    public void Add(KurinAuditEvent entry) => _context.KurinAuditEvents.Add(entry);
}
