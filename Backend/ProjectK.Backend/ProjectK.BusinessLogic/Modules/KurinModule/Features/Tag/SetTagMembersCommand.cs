using ProjectK.Common.Exceptions;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Tag;

/// <summary>
/// Makes <paramref name="MemberKeys"/> exactly the people who carry a tag: puts it on whoever is new
/// and takes it off whoever is left out. Everyone named must be in the kurin now, and nobody may end
/// up with more than the limit — then nothing changes at all.
/// </summary>
public sealed record SetTagMembersCommand(Guid KurinKey, Guid KurinTagKey, IReadOnlyCollection<Guid> MemberKeys)
    : IRequest<ServiceResult<KurinTagDto>>;

public sealed class SetTagMembersCommandHandler : IRequestHandler<SetTagMembersCommand, ServiceResult<KurinTagDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IKurinTagVisibility _visibility;
    private readonly KurinTagAssignments _assignments;

    public SetTagMembersCommandHandler(IUnitOfWork uow, IKurinTagVisibility visibility, IKurinAudit audit)
    {
        _uow = uow;
        _visibility = visibility;
        _assignments = new KurinTagAssignments(uow, audit);
    }

    public async Task<ServiceResult<KurinTagDto>> Handle(SetTagMembersCommand request, CancellationToken cancellationToken)
    {
        var settings = await _visibility.SettingsAsync(request.KurinKey, cancellationToken);
        if (settings is not { Enabled: true })
        {
            return ServiceResult<KurinTagDto>.Failure(ResultType.Conflict, KurinTagErrors.TagsDisabled, "The kurin has tags turned off.");
        }

        var tag = await _uow.KurinTags.GetWithMembersAsync(request.KurinTagKey, cancellationToken);
        if (tag is null || tag.KurinKey != request.KurinKey)
        {
            return ServiceResult<KurinTagDto>.Failure(ResultType.NotFound, KurinTagErrors.TagNotFound, "Tag was not found in this kurin.");
        }

        var wanted = request.MemberKeys.Distinct().ToHashSet();
        // Who left keeps the tag for a return and is not in the list that was sent: leave them be.
        var former = (await _assignments.OutsideKurinAsync(request.KurinKey, tag.Members.Select(a => a.MemberKey).ToList(), cancellationToken)).ToHashSet();
        var carrying = tag.Members.Where(a => !former.Contains(a.MemberKey)).ToDictionary(a => a.MemberKey);
        var gaining = wanted.Where(key => !carrying.ContainsKey(key)).ToList();

        if ((await _assignments.OutsideKurinAsync(request.KurinKey, gaining, cancellationToken)).Count > 0)
        {
            return ServiceResult<KurinTagDto>.Failure(ResultType.BadRequest, KurinTagErrors.NotInKurin, "Everyone given a tag must be in the kurin now.");
        }

        await using var transaction = await _assignments.HoldAsync(gaining, cancellationToken);
        if ((await _assignments.OverLimitAsync(request.KurinKey, gaining, cancellationToken)).Count > 0)
        {
            return ServiceResult<KurinTagDto>.Failure(ResultType.BadRequest, KurinTagErrors.TooManyTags, "A person would carry more tags than the limit.");
        }

        foreach (var memberKey in gaining)
        {
            await _assignments.PutAsync(tag, memberKey, cancellationToken);
        }

        foreach (var assignment in carrying.Values.Where(a => !wanted.Contains(a.MemberKey)).ToList())
        {
            await _assignments.TakeOffAsync(tag, assignment, cancellationToken);
        }

        try
        {
            await _uow.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateRowException)
        {
            return KurinTagErrors.ChangedMeanwhile<KurinTagDto>();
        }

        await transaction.CommitAsync(cancellationToken);

        return new ServiceResult<KurinTagDto>(ResultType.Success,
            new KurinTagDto(tag.KurinTagKey, tag.Name, tag.ColorHex, wanted.Count));
    }
}
