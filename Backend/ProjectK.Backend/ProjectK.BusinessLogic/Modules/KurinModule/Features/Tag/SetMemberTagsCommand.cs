using ProjectK.Common.Exceptions;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Tags;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Tag;

/// <summary>
/// Makes <paramref name="TagKeys"/> exactly the kurin's tags a person carries, in the kurin the caller
/// acts in. An administrator who has not chosen a kurin manages tags anywhere (as
/// <see cref="IKurinTagVisibility.CanManageAsync"/> lets them): they name the kurin, or the person's
/// only kurin is taken. Answers with the tags they carry after it.
/// </summary>
public sealed record SetMemberTagsCommand(Guid MemberKey, IReadOnlyCollection<Guid> TagKeys, Guid? KurinKey = null)
    : IRequest<ServiceResult<IReadOnlyList<Guid>>>;

public sealed class SetMemberTagsCommandHandler : IRequestHandler<SetMemberTagsCommand, ServiceResult<IReadOnlyList<Guid>>>
{
    private readonly IUnitOfWork _uow;
    private readonly IKurinTagVisibility _visibility;
    private readonly KurinTagAssignments _assignments;
    private readonly ICurrentUserContext _currentUser;

    public SetMemberTagsCommandHandler(
        IUnitOfWork uow,
        IKurinTagVisibility visibility,
        IKurinAudit audit,
        ICurrentUserContext currentUser)
    {
        _uow = uow;
        _visibility = visibility;
        _assignments = new KurinTagAssignments(uow, audit);
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<Guid>>> Handle(SetMemberTagsCommand request, CancellationToken cancellationToken)
    {
        // Tags are a kurin's, and the person may be in several: these are the ones of where the caller acts.
        if ((_currentUser.KurinKey ?? await UnscopedAdminKurinAsync(request, cancellationToken)) is not { } kurinKey)
        {
            return ServiceResult<IReadOnlyList<Guid>>.Failure(ResultType.BadRequest, KurinTagErrors.NotInKurin, "Tags are set from inside a kurin.");
        }

        var settings = await _visibility.SettingsAsync(kurinKey, cancellationToken);
        if (settings is not { Enabled: true })
        {
            return ServiceResult<IReadOnlyList<Guid>>.Failure(ResultType.Conflict, KurinTagErrors.TagsDisabled, "The kurin has tags turned off.");
        }

        var wanted = request.TagKeys.Distinct().ToHashSet();
        if (wanted.Count > KurinTagRules.MaxTagsPerMember)
        {
            return ServiceResult<IReadOnlyList<Guid>>.Failure(ResultType.BadRequest, KurinTagErrors.TooManyTags, "A person would carry more tags than the limit.");
        }

        if ((await _assignments.OutsideKurinAsync(kurinKey, [request.MemberKey], cancellationToken)).Count > 0)
        {
            return ServiceResult<IReadOnlyList<Guid>>.Failure(ResultType.BadRequest, KurinTagErrors.NotInKurin, "The person is not in the kurin now.");
        }

        var tags = (await _uow.KurinTags.GetForKurinAsync(kurinKey, cancellationToken)).ToDictionary(t => t.KurinTagKey);
        if (wanted.Any(key => !tags.ContainsKey(key)))
        {
            return ServiceResult<IReadOnlyList<Guid>>.Failure(ResultType.BadRequest, KurinTagErrors.TagNotFound, "A tag was not found in this kurin.");
        }

        await using var transaction = await _assignments.HoldAsync([request.MemberKey], cancellationToken);
        var carrying = await _uow.KurinTags.GetTrackedForMemberAsync(kurinKey, request.MemberKey, cancellationToken);
        var carried = carrying.Select(a => a.KurinTagKey).ToHashSet();

        foreach (var tagKey in wanted.Where(key => !carried.Contains(key)))
        {
            await _assignments.PutAsync(tags[tagKey], request.MemberKey, cancellationToken);
        }

        foreach (var assignment in carrying.Where(a => !wanted.Contains(a.KurinTagKey)))
        {
            await _assignments.TakeOffAsync(tags[assignment.KurinTagKey], assignment, cancellationToken);
        }

        try
        {
            await _uow.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateRowException)
        {
            return KurinTagErrors.ChangedMeanwhile<IReadOnlyList<Guid>>();
        }

        await transaction.CommitAsync(cancellationToken);

        return new ServiceResult<IReadOnlyList<Guid>>(ResultType.Success, [.. wanted]);
    }

    /// <summary>The kurin an administrator outside any kurin means: the one they name, or the person's only one.</summary>
    private async Task<Guid?> UnscopedAdminKurinAsync(SetMemberTagsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole(SystemRole.Admin))
        {
            return null;
        }

        if (request.KurinKey is { } named)
        {
            return named;
        }

        var standing = await _uow.Memberships.GetActiveForMemberAsync(request.MemberKey, cancellationToken);
        return standing.Count == 1 ? standing.First().KurinKey : null;
    }
}
