namespace ProjectK.Common.Contracts.AuditModule;

/// <summary>
/// The kurin journal's way to its table, kept off <c>IUnitOfWork</c> like <c>IDuesUnitOfWork</c>.
/// Writing is the journal's own (<see cref="IKurinAudit"/> stamps the actor and strips private data);
/// reading is the audit module's query and the progress trails'. The same instance backs every
/// facade, so an entry commits with whoever saves the request.
/// </summary>
public interface IAuditUnitOfWork
{
    IKurinAuditRepository KurinAudit { get; }

    IKurinJournalRepository KurinJournal { get; }
}
