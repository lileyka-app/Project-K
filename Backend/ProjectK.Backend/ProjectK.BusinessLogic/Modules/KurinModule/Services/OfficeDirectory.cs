using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Models.Authorization;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <inheritdoc />
public sealed class OfficeDirectory : IOfficeDirectory
{
    private readonly IUnitOfWork _unitOfWork;

    public OfficeDirectory(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyCollection<MemberOffice>> GetForAccountInKurinAsync(
        Guid userKey,
        Guid kurinKey,
        CancellationToken cancellationToken = default)
        => await _unitOfWork.Leaderships.GetActiveOfficesForAccountInKurinAsync(
            userKey, kurinKey, cancellationToken);
}
