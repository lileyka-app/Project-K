using ProjectK.Common.Models.Members;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using MemberEntity = ProjectK.Common.Entities.KurinModule.Member;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member;

public class UpsertMemberCommand : IRequest<ServiceResult<MemberDto>>, IHasParentContacts
{
    public Guid MemberKey { get; set; }
    public Guid? UserKey { get; set; }
    public Guid? KurinKey { get; set; }
    public Guid? GroupKey { get; set; }
    public bool CreateUserAccount { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? School { get; set; }
    public string? MotherFirstName { get; set; }
    public string? MotherLastName { get; set; }
    public string? MotherPhone { get; set; }
    public string? FatherFirstName { get; set; }
    public string? FatherLastName { get; set; }
    public string? FatherPhone { get; set; }
    public ICollection<PlastLevelHistoryDto> PlastLevelHistories { get; set; } = [];

    /// <inheritdoc cref="UpsertMemberProfileCommand.CardExtras"/>
    public IDictionary<string, string>? CardExtras { get; set; }

    /// <inheritdoc cref="UpsertMemberProfileCommand.ApplyCardFields"/>
    public bool ApplyCardFields { get; set; }
    public bool RemoveProfilePhoto { get; set; }
    public Stream? BlobContent { get; set; }
    public string? BlobFileName { get; set; }
    public string? BlobContentType { get; set; }
}

/// <summary>
/// The one multipart form the UI submits reaches three separate use cases — the profile, the
/// photo and the account. This handler only decides the order and reports the outcome; every
/// rule about a member's data lives in the use case that owns it.
/// </summary>
public class UpsertMemberCommandHandler : IRequestHandler<UpsertMemberCommand, ServiceResult<MemberDto>>
{
    private readonly IMediator _mediator;
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly IBlobReadLinks _links;
    private readonly IAccountProvisioningService _accountProvisioning;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IDomainEventPublisher _events;
    private readonly IMemberFieldAccess _fieldAccess;

    public UpsertMemberCommandHandler(
        IMediator mediator,
        IMemberUnitOfWork unitOfWork,
        IBlobReadLinks links,
        IAccountProvisioningService accountProvisioning,
        ICurrentUserContext currentUserContext,
        IDomainEventPublisher events,
        IMemberFieldAccess fieldAccess)
    {
        _fieldAccess = fieldAccess;
        _mediator = mediator;
        _unitOfWork = unitOfWork;
        _links = links;
        _accountProvisioning = accountProvisioning;
        _currentUserContext = currentUserContext;
        _events = events;
    }

    public async Task<ServiceResult<MemberDto>> Handle(
        UpsertMemberCommand request,
        CancellationToken cancellationToken)
    {
        var accountLink = await _unitOfWork.Members.GetAccountLinkAsync(request.MemberKey, cancellationToken);

        // Only leadership hands out accounts, and never twice. Both are settled before anything
        // is written: a member created here and then refused an account would be a half-result
        // nobody asked for.
        var provisionAccount = request.CreateUserAccount
            && (accountLink is null || _currentUserContext.IsLeadership());

        if (provisionAccount)
        {
            if (accountLink?.UserKey is not null)
            {
                return new ServiceResult<MemberDto>(ResultType.Conflict);
            }

            var availability = await _accountProvisioning.CheckAvailabilityAsync(request.Email, cancellationToken);
            if (availability != AccountAvailability.Available)
            {
                return new ServiceResult<MemberDto>(ResultType.Conflict);
            }
        }

        var profile = await _mediator.Send(request.ToProfileCommand(), cancellationToken);
        if (profile.Type != ResultType.Success || profile.Data is null)
        {
            return Propagate(profile);
        }

        if (request.BlobContent is not null || request.RemoveProfilePhoto)
        {
            var photo = await _mediator.Send(
                new SetMemberPhotoCommand(
                    profile.Data.MemberKey,
                    request.BlobContent,
                    request.BlobFileName,
                    request.RemoveProfilePhoto),
                cancellationToken);

            if (photo.Type != ResultType.Success)
            {
                return Propagate(photo);
            }
        }

        bool? invitationSent = null;

        if (profile.Data.AccountEmailToFollow)
        {
            var moved = await _mediator.Send(
                new MoveMemberAccountEmailCommand(profile.Data.MemberKey),
                cancellationToken);

            // The profile is already written; a letter that did not go is reported, not failed on.
            if (moved.ErrorCode == ResendMemberInvitationCommandHandler.InvitationNotSent)
            {
                invitationSent = false;
            }
            else if (moved.Type != ResultType.Success)
            {
                return Propagate(moved);
            }
        }

        if (provisionAccount)
        {
            var account = await _mediator.Send(
                new ProvisionMemberAccountCommand(profile.Data.MemberKey),
                cancellationToken);

            if (account.Type != ResultType.Success || account.Data is null)
            {
                return Propagate(account);
            }

            invitationSent = account.Data.Sent;
        }

        var member = await _unitOfWork.Members.GetByKeyAsync(profile.Data.MemberKey, cancellationToken);
        if (member is null)
        {
            return new ServiceResult<MemberDto>(ResultType.InternalServerError);
        }

        if (!profile.Data.IsCreated
            && profile.Data.WasProfileVerifiedCurrent
            && member.ProfileVerificationStatus == MemberProfileVerificationStatus.VerifiedStale)
        {
            await PublishProfileWentStaleAsync(member, cancellationToken);
        }

        // The writer is answered like any reader: what they may not see stays kept, and unsaid.
        var response = member.ToDto(_links)
            .MaskFor(await _fieldAccess.ForViewerAsync(cancellationToken), profile.Data.GroupKey);
        response.InvitationSent = invitationSent;

        return profile.Data.IsCreated
            ? new ServiceResult<MemberDto>(
                ResultType.Created,
                response,
                CreatedAtActionName: "GetByKey",
                CreatedAtRouteValues: new { memberKey = response.MemberKey })
            : new ServiceResult<MemberDto>(ResultType.Success, response);
    }

    private static ServiceResult<MemberDto> Propagate<T>(ServiceResult<T> step) =>
        step.ErrorCode is null
            ? new ServiceResult<MemberDto>(step.Type)
            : ServiceResult<MemberDto>.Failure(step.Type, step.ErrorCode, step.ErrorMessage!);

    private Task PublishProfileWentStaleAsync(
        MemberEntity member,
        CancellationToken cancellationToken) =>
        _events.PublishAsync(
            new MemberProfileWentStale(
                member.MemberKey,
                $"{member.FirstName} {member.LastName}".Trim(),
                _currentUserContext.UserId),
            cancellationToken);

}
