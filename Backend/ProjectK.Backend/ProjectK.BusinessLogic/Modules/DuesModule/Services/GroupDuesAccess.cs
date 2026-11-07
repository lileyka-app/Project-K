using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>
/// The гурток every dues request is about, checked to stand in the kurin the caller acts in. The
/// endpoint's authorization already pins both; this is the handler's own answer to "does it exist",
/// and it keeps the kurin key out of every request body.
/// </summary>
public sealed class GroupDuesAccess
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly IDuesKurinShape _shape;

    public GroupDuesAccess(IUnitOfWork unitOfWork, ICurrentUserContext currentUser, IDuesKurinShape shape)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _shape = shape;
    }

    public async Task<(Group? Group, ServiceResult<T>? Failure)> OpenAsync<T>(Guid groupKey, CancellationToken cancellationToken)
    {
        var group = await _unitOfWork.Groups.GetByKeyAsync(groupKey, cancellationToken);
        if (group is null)
        {
            return (null, ServiceResult<T>.Failure(ResultType.NotFound, "GroupNotFound", "There is no such гурток."));
        }

        if (_currentUser.KurinKey != group.KurinKey)
        {
            return (null, new ServiceResult<T>(ResultType.Forbidden));
        }

        return (group, null);
    }

    /// <summary>
    /// The box a person's account is kept in: a гурток by its key, or — given the key of the kurin the
    /// caller acts in, when that kurin has no гуртки — the kurin's own box.
    /// </summary>
    public async Task<(DuesBox? Box, ServiceResult<T>? Failure)> OpenBoxAsync<T>(Guid boxKey, CancellationToken cancellationToken)
    {
        if (boxKey == _currentUser.KurinKey)
        {
            return await _shape.IsSeniorAsync(boxKey, cancellationToken)
                ? (new DuesBox(boxKey, null), null)
                : (null, ServiceResult<T>.Failure(ResultType.NotFound, "GroupNotFound", "There is no such гурток."));
        }

        var (group, failure) = await OpenAsync<T>(boxKey, cancellationToken);
        return group is null ? (null, failure) : (new DuesBox(group.KurinKey, group.GroupKey), null);
    }
}

/// <summary>Where a person's вкладка is kept: a гурток's box, or with no <see cref="GroupKey"/> the kurin's own.</summary>
public sealed record DuesBox(Guid KurinKey, Guid? GroupKey);
