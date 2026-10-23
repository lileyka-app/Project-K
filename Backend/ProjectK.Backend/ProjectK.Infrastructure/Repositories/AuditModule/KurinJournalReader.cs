using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Interfaces.Modules.AuditModule;
using ProjectK.Common.Models.Records;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.AuditModule;

public sealed class KurinJournalReader : IKurinJournalReader
{
    private readonly AppDbContext _context;

    public KurinJournalReader(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<KurinAuditEvent>> ReadAsync(
        KurinJournalFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _context.KurinAuditEvents
            .AsNoTracking()
            .Where(entry => entry.KurinKey == filter.KurinKey);

        if (filter.Area is { } area)
        {
            query = query.Where(entry => entry.Area == area);
        }

        if (filter.MemberKey is { } memberKey)
        {
            query = query.Where(entry => entry.SubjectMemberKey == memberKey);
        }

        if (filter.ActorUserKey is { } actorUserKey)
        {
            query = query.Where(entry => entry.ActorUserKey == actorUserKey);
        }

        if (filter.FromUtc is { } fromUtc)
        {
            query = query.Where(entry => entry.OccurredAtUtc >= fromUtc);
        }

        if (filter.ToUtc is { } toUtc)
        {
            query = query.Where(entry => entry.OccurredAtUtc < toUtc);
        }

        if (filter.Visibility is { } visibility)
        {
            var groupKeys = visibility.GroupKeys.ToList();
            var ownMemberKey = visibility.OwnMemberKey;
            query = query.Where(entry =>
                (entry.SubjectGroupKey != null && groupKeys.Contains(entry.SubjectGroupKey.Value))
                || (ownMemberKey != null && entry.SubjectMemberKey == ownMemberKey));
        }

        return await query
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ThenByDescending(entry => entry.KurinAuditEventKey)
            .Skip(Math.Max(0, skip))
            .Take(Math.Max(0, take))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KurinAuditEvent>> GetForSubjectsAsync(
        IReadOnlyCollection<Guid> subjectKeys,
        CancellationToken cancellationToken = default)
    {
        if (subjectKeys.Count == 0)
        {
            return [];
        }

        var keys = subjectKeys.Distinct().ToList();
        return await _context.KurinAuditEvents
            .AsNoTracking()
            .Where(entry => keys.Contains(entry.SubjectKey))
            .OrderBy(entry => entry.OccurredAtUtc)
            .ThenBy(entry => entry.KurinAuditEventKey)
            .ToListAsync(cancellationToken);
    }
}
