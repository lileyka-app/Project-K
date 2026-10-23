using Moq;
using ProjectK.BusinessLogic.Modules.AuditModule.Services;
using ProjectK.BusinessLogic.Services.Audit;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuditModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>
/// The real journal — actor, subject and redaction worked out by <see cref="KurinAudit"/> — with the
/// write caught in a list instead of a database, and the people it names held here.
/// </summary>
internal sealed class JournalRecorder : IKurinAuditWriter
{
    private readonly List<MemberSummary> _people = [];

    public JournalRecorder(ICurrentUserContext user, AuditActorResolver? actors = null)
    {
        People.Setup(x => x.GetByKurinAsync(It.IsAny<Guid>(), It.IsAny<MemberSelection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid kurinKey, MemberSelection selection, CancellationToken _) =>
                _people.Where(p => p.KurinKey == kurinKey && selection.MemberKeys.Contains(p.MemberKey)).ToList());
        People.Setup(x => x.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid memberKey, CancellationToken _) => _people.FirstOrDefault(p => p.MemberKey == memberKey));
        People.Setup(x => x.FindByAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userKey, CancellationToken _) => _people.FirstOrDefault(p => p.UserKey == userKey));

        Actors = actors ?? new AuditActorResolver(user, People.Object);
        Audit = new KurinAudit(Actors, user, People.Object, this);
    }

    /// <summary>What reached the writer, in order.</summary>
    public List<KurinAuditEvent> Entries { get; } = [];

    /// <summary>The directory the journal names people from.</summary>
    public Mock<IMemberDirectory> People { get; } = new();

    public AuditActorResolver Actors { get; }

    public IKurinAudit Audit { get; }

    /// <summary>Someone the journal can name: their kurin and гурток as they stand now.</summary>
    public JournalRecorder Knows(Guid memberKey, Guid kurinKey, Guid? groupKey, string firstName, string lastName, Guid? userKey = null)
    {
        _people.Add(new MemberSummary(memberKey, userKey, kurinKey, groupKey, firstName, lastName, $"{memberKey:N}@example.com", null));
        return this;
    }

    public void Add(KurinAuditEvent entry) => Entries.Add(entry);
}
