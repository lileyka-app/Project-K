using Moq;
using ProjectK.BusinessLogic.Modules.AuditModule.Services;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>
/// The real journal — actor, subject and redaction worked out by <see cref="KurinAudit"/> — with the
/// write caught in a list instead of a database, and the people it names held here.
/// </summary>
internal sealed class JournalRecorder : IKurinAuditRepository, IAuditUnitOfWork
{
    private readonly List<KurinPerson> _people = [];

    public JournalRecorder(ICurrentUserContext user, CurrentMember? actors = null)
    {
        Placements.Setup(x => x.GetPeopleInKurinAsync(It.IsAny<Guid>(), It.IsAny<MemberSelection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid kurinKey, MemberSelection selection, CancellationToken _) =>
                _people.Where(p => p.KurinKey == kurinKey && selection.MemberKeys.Contains(p.MemberKey)).ToList());
        Placements.Setup(x => x.FindInLatestKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid memberKey, CancellationToken _) => _people.FirstOrDefault(p => p.MemberKey == memberKey));
        People.Setup(x => x.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid memberKey, CancellationToken _) => _people.FirstOrDefault(p => p.MemberKey == memberKey)?.Person);
        People.Setup(x => x.FindByAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userKey, CancellationToken _) => _people.FirstOrDefault(p => p.UserKey == userKey)?.Person);

        Actors = actors ?? new CurrentMember(user, People.Object, Mock.Of<IMembershipDirectory>());
        Audit = new KurinAudit(Actors, user, People.Object, Placements.Object, this);
    }

    /// <summary>What reached the writer, in order.</summary>
    public List<KurinAuditEvent> Entries { get; } = [];

    /// <summary>The directory the journal names people from.</summary>
    public Mock<IMemberDirectory> People { get; } = new();

    /// <summary>The directory the journal places people from: their kurin and гурток.</summary>
    public Mock<IMembershipDirectory> Placements { get; } = new();

    public CurrentMember Actors { get; }

    public IKurinAudit Audit { get; }

    /// <summary>Someone the journal can name: their kurin and гурток as they stand now.</summary>
    public JournalRecorder Knows(Guid memberKey, Guid kurinKey, Guid? groupKey, string firstName, string lastName, Guid? userKey = null)
    {
        _people.Add(KurinPeople.Of(memberKey, userKey, kurinKey, groupKey, firstName, lastName, $"{memberKey:N}@example.com", null));
        return this;
    }

    public void Add(KurinAuditEvent entry) => Entries.Add(entry);

    /// <summary>Every key set a handler asked the journal about, one per read.</summary>
    public List<IReadOnlyCollection<Guid>> SubjectReads { get; } = [];

    /// <summary>The journal's unit of work: writes land in <see cref="Entries"/>, reads come back from them.</summary>
    public IAuditUnitOfWork Reader => this;

    public IKurinAuditRepository KurinAudit => this;

    /// <summary>The journal read back: what was recorded, as the database would return it.</summary>
    public IKurinJournalRepository KurinJournal => new RecordedJournal(this);

    private sealed class RecordedJournal(JournalRecorder recorder) : IKurinJournalRepository
    {
        public Task<IReadOnlyList<KurinAuditEvent>> ReadAsync(
            KurinJournalFilter filter, int skip, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KurinAuditEvent>>(recorder.Entries
                .Where(entry => entry.KurinKey == filter.KurinKey)
                .OrderByDescending(entry => entry.OccurredAtUtc)
                .Skip(skip)
                .Take(take)
                .ToList());

        public Task<IReadOnlyList<KurinAuditEvent>> GetForSubjectsAsync(
            IReadOnlyCollection<Guid> subjectKeys, CancellationToken cancellationToken = default)
        {
            recorder.SubjectReads.Add(subjectKeys.ToList());
            return Task.FromResult<IReadOnlyList<KurinAuditEvent>>(recorder.Entries
                .Where(entry => subjectKeys.Contains(entry.SubjectKey))
                .OrderBy(entry => entry.OccurredAtUtc)
                .ToList());
        }
    }
}
