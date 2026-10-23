using ProjectK.Common.Entities.AuditModule;

namespace ProjectK.Common.Interfaces.Modules.AuditModule;

/// <summary>
/// Puts a journal entry on the request's unit of work without saving it, so it commits with the
/// handler's own save. Used only by the journal itself; handlers go through <see cref="IKurinAudit"/>.
/// </summary>
public interface IKurinAuditWriter
{
    void Add(KurinAuditEvent entry);
}
