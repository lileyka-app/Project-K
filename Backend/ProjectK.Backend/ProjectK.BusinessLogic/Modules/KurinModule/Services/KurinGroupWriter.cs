using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>Opens гуртки for another module's use case, inside that use case's transaction.</summary>
public sealed class KurinGroupWriter : IKurinGroupWriter
{
    private readonly IUnitOfWork _unitOfWork;

    public KurinGroupWriter(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyDictionary<string, Guid>> OpenAsync(Guid kurinKey, IReadOnlyCollection<string> names, CancellationToken cancellationToken = default)
    {
        var opened = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var group = new Group(name, kurinKey);
            _unitOfWork.Groups.Create(group, cancellationToken);
            opened[name] = group.GroupKey;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return opened;
    }
}
