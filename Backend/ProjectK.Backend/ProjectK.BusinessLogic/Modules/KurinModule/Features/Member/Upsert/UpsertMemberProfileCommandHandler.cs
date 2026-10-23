using ProjectK.Common.Models.Members;
using ProjectK.Common.Phones;
using AutoMapper;
using MediatR;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using GroupEntity = ProjectK.Common.Entities.KurinModule.Group;
using MemberEntity = ProjectK.Common.Entities.KurinModule.Member;
using MembershipEntity = ProjectK.Common.Entities.KurinModule.Membership;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Upsert;

public class UpsertMemberProfileCommandHandler
    : IRequestHandler<UpsertMemberProfileCommand, ServiceResult<MemberProfileWriteResult>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    // Placement is the kurin's to record, so writing a member still means asking the kurin
    // which гурток is meant and telling it where the person now stands.
    private readonly IUnitOfWork _kurinData;
    private readonly IMembershipRepository _memberships;
    private readonly IMapper _mapper;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IDomainEventPublisher _events;
    private readonly IAccountProvisioningService _accounts;
    private readonly IKraiProfileProvider _profiles;
    private readonly IMemberFieldAccess _fieldAccess;

    /// <summary>The error a write without an email is refused with.</summary>
    public const string EmailRequired = "EmailRequired";

    public UpsertMemberProfileCommandHandler(
        IMemberUnitOfWork unitOfWork,
        IUnitOfWork kurinData,
        IMapper mapper,
        ICurrentUserContext currentUserContext,
        IDomainEventPublisher events,
        IAccountProvisioningService accounts,
        IKraiProfileProvider profiles,
        IMemberFieldAccess fieldAccess)
    {
        _fieldAccess = fieldAccess;
        _accounts = accounts;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
        _kurinData = kurinData;
        _memberships = kurinData.Memberships;
        _mapper = mapper;
        _currentUserContext = currentUserContext;
        _events = events;
    }

    public async Task<ServiceResult<MemberProfileWriteResult>> Handle(
        UpsertMemberProfileCommand request,
        CancellationToken cancellationToken)
    {
        var existing = await _unitOfWork.Members.GetByKeyAsync(request.MemberKey, cancellationToken);

        // Where they already stand. The record does not say any more, so it is read from the
        // membership — and it is what the request is measured against.
        var current = existing is null
            ? null
            : (await _memberships.GetActiveForMemberAsync(request.MemberKey, cancellationToken))
                .FirstOrDefault();

        // Where a person stands is not a field of their profile. Editing an existing person takes
        // the placement back from the membership they already have — whoever is editing, and
        // whatever the request said. Moving someone is a membership action, with its own route
        // and its own right; an edit form must not be able to do it by accident.
        if (current is not null)
        {
            request.GroupKey = current.GroupKey;
            request.KurinKey = current.KurinKey;
        }

        var hiddenFromWriter = existing is null
            ? new HashSet<MemberDataGroup>()
            : (await _fieldAccess.ForViewerAsync(cancellationToken)).HiddenFrom(existing.UserKey, current?.GroupKey);
        if (existing is not null)
        {
            HiddenDataOnWrite.KeepStored(hiddenFromWriter, request, existing);
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return ServiceResult<MemberProfileWriteResult>.Failure(ResultType.BadRequest, EmailRequired, "An email is required.");
        }

        GroupEntity? group = null;
        if (request.GroupKey.HasValue && request.GroupKey.Value != Guid.Empty)
        {
            group = await _kurinData.Groups.GetByKeyAsync(request.GroupKey.Value, cancellationToken);
        }

        if (group == null && (!request.KurinKey.HasValue || request.KurinKey.Value == Guid.Empty))
        {
            return new ServiceResult<MemberProfileWriteResult>(ResultType.NotFound);
        }

        // Where they are to stand once this is written.
        var kurinKey = group?.KurinKey ?? request.KurinKey!.Value;
        var groupKey = group?.GroupKey;

        var cardExtras = existing?.CardExtras;
        if (request.ApplyCardFields && await _profiles.GetForKurinAsync(kurinKey, cancellationToken) is { } profile)
        {
            var card = MemberCardFields.Apply(profile.CardFields, request, existing, hiddenFromWriter);
            if (card.Error is not null)
            {
                return ServiceResult<MemberProfileWriteResult>.Failure(ResultType.BadRequest, MemberCardFields.ErrorCode, card.Error);
            }

            cardExtras = card.CardExtras;
        }
        else if (existing is not null)
        {
            // A write that does not carry the parents' contacts (the roster import) keeps them.
            foreach (var field in ParentContacts.Fields)
            {
                field.Set(request, field.Get(request) ?? field.Get(existing));
            }
        }

        // Every write stores the phone as +380501234567. One the member already has is let through
        // as it is, so a number older than the rule does not block an edit of something else.
        if (existing is null || !string.Equals(request.PhoneNumber, existing.PhoneNumber, StringComparison.Ordinal))
        {
            var phone = PhoneNumbers.Normalize(request.PhoneNumber);
            if (phone is null)
            {
                return ServiceResult<MemberProfileWriteResult>.Failure(ResultType.BadRequest, PhoneNumbers.ErrorCode, PhoneNumbers.InvalidMessage);
            }

            request.PhoneNumber = phone;
        }

        // A parent's phone is stored in the same shape; an empty one stays empty.
        if (!TryNormalizeParentPhone(request.MotherPhone, existing?.MotherPhone, out var motherPhone)
            || !TryNormalizeParentPhone(request.FatherPhone, existing?.FatherPhone, out var fatherPhone))
        {
            return ServiceResult<MemberProfileWriteResult>.Failure(ResultType.BadRequest, PhoneNumbers.ErrorCode, PhoneNumbers.InvalidMessage);
        }

        request.MotherPhone = motherPhone;
        request.FatherPhone = fatherPhone;

        var wasProfileVerifiedCurrent =
            existing?.ProfileVerificationStatus == MemberProfileVerificationStatus.VerifiedCurrent;

        bool isCreated;
        string? previousPhotoBlobName = null;
        var accountEmailToFollow = false;

        if (existing == null)
        {
            existing = _mapper.Map<MemberEntity>(request);
            existing.CardExtras = cardExtras;

            // The ступені the request carried. The mapper deliberately leaves the collection
            // alone — it cannot tell a new row from an edited one — so a person created with
            // their ступені known arrived with none of them until this ran. The roster import is
            // what made that visible: every imported юнак came in with a blank ступінь.
            if (CanEditRestrictedFields())
            {
                UpdatePlastLevelHistory(
                    existing.MemberKey,
                    kurinKey,
                    request.PlastLevelHistories,
                    existing.PlastLevelHistory);
            }

            existing.LatestPlastLevel = LatestLevelOf(existing.PlastLevelHistory);

            _unitOfWork.Members.Create(existing, cancellationToken);
            isCreated = true;
        }
        else
        {
            isCreated = false;
            var shouldMarkProfileStale = wasProfileVerifiedCurrent
                && (HasSignificantProfileChange(request, existing, current, kurinKey, groupKey)
                    || !string.Equals(existing.CardExtras, cardExtras, StringComparison.Ordinal));

            var preserveLinkedUserEmail = false;
            string? linkedUserEmail = null;

            if (existing.UserKey.HasValue)
            {
                var emailChanged = !string.Equals(existing.Email, request.Email, StringComparison.OrdinalIgnoreCase);
                var phoneChanged = !string.Equals(existing.PhoneNumber, request.PhoneNumber, StringComparison.OrdinalIgnoreCase);

                if ((emailChanged || phoneChanged) && !CanEditRestrictedFields() && !IsCurrentUserOwner(existing))
                {
                    return ServiceResult<MemberProfileWriteResult>.Failure(
                        ResultType.BadRequest,
                        "ContactInfoLinked",
                        "Cannot change email or phone number for a member linked to an active user account. The user must update this via their account settings.");
                }

                if (emailChanged)
                {
                    // The member and the account sign in under one address, and activation finds the
                    // member by it. An account nobody has claimed yet follows the record — its letter
                    // went to the old address, which is usually the very mistake being fixed. One in
                    // use belongs to its owner, and only an admin may move it for them.
                    var account = await _accounts.FindAsync(existing.UserKey.Value, cancellationToken);
                    accountEmailToFollow = account is not null
                        && (account.Status == OnboardingStatus.PendingActivation || IsAdmin());

                    if (accountEmailToFollow
                        && await _accounts.CheckAvailabilityAsync(request.Email, cancellationToken) != AccountAvailability.Available)
                    {
                        return ServiceResult<MemberProfileWriteResult>.Failure(
                            ResultType.Conflict,
                            "EmailTaken",
                            "This email is already used by another account or registration.");
                    }
                }

                preserveLinkedUserEmail = emailChanged && !accountEmailToFollow && !IsAdmin();
                linkedUserEmail = preserveLinkedUserEmail ? existing.Email : null;
            }

            previousPhotoBlobName = existing.ProfilePhotoBlobName;
            _mapper.Map(request, existing);
            existing.CardExtras = cardExtras;

            if (preserveLinkedUserEmail)
            {
                existing.Email = linkedUserEmail!;
            }

            if (shouldMarkProfileStale)
            {
                existing.ProfileVerificationStatus = MemberProfileVerificationStatus.VerifiedStale;
            }

            if (CanEditPlastLevels(existing))
            {
                UpdatePlastLevelHistory(existing.MemberKey, kurinKey, request.PlastLevelHistories, existing.PlastLevelHistory);
                existing.LatestPlastLevel = LatestLevelOf(existing.PlastLevelHistory);
            }

            _unitOfWork.Members.Update(existing, cancellationToken);
        }

        // Where the person stands is the kurin's to record, and it is the only place it is
        // written. The member module says who was put where; the kurin opens or moves the
        // membership in response, inside this same unit of work.
        await _events.PublishAsync(
            new MemberPlaced(existing.MemberKey, existing.UserKey, kurinKey, groupKey),
            cancellationToken);

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            return new ServiceResult<MemberProfileWriteResult>(ResultType.InternalServerError);
        }

        return new ServiceResult<MemberProfileWriteResult>(
            ResultType.Success,
            new MemberProfileWriteResult(
                existing.MemberKey,
                isCreated,
                wasProfileVerifiedCurrent,
                previousPhotoBlobName,
                accountEmailToFollow,
                groupKey));
    }

    private bool CanEditRestrictedFields() => _currentUserContext.IsLeadership();

    private bool IsAdmin() => _currentUserContext.IsAdmin();

    // A member records their own ступені too; a change to them counts as a significant one, so a
    // verified profile goes stale until the провід checks it again.
    private bool CanEditPlastLevels(MemberEntity member) => CanEditRestrictedFields() || IsCurrentUserOwner(member);

    private bool IsCurrentUserOwner(MemberEntity member) =>
        member.UserKey.HasValue
        && _currentUserContext.UserId.HasValue
        && member.UserKey.Value == _currentUserContext.UserId.Value;

    private static PlastLevel? LatestLevelOf(ICollection<PlastLevelHistory> history) =>
        history.OrderByDescending(entry => entry.DateAchieved).FirstOrDefault()?.PlastLevel;

    private static void UpdatePlastLevelHistory(
        Guid memberKey,
        Guid? kurinKey,
        ICollection<PlastLevelHistoryDto> plastLevelHistoryDto,
        ICollection<PlastLevelHistory> plastLevelHistory)
    {
        if (plastLevelHistoryDto == null || !plastLevelHistoryDto.Any())
        {
            plastLevelHistory.Clear();
            return;
        }

        var dtoDict = plastLevelHistoryDto
            .Where(dto => dto.PlastLevelHistoryKey.HasValue && dto.PlastLevelHistoryKey != Guid.Empty)
            .ToDictionary(dto => dto.PlastLevelHistoryKey!.Value);

        var entitiesToDelete = plastLevelHistory
            .Where(e => !dtoDict.ContainsKey(e.PlastLevelHistoryKey))
            .ToList();

        foreach (var entity in entitiesToDelete)
        {
            plastLevelHistory.Remove(entity);
        }

        foreach (var dto in plastLevelHistoryDto)
        {
            if (!dto.PlastLevelHistoryKey.HasValue || dto.PlastLevelHistoryKey == Guid.Empty)
            {
                plastLevelHistory.Add(new PlastLevelHistory
                {
                    MemberKey = memberKey,
                    KurinKey = kurinKey,
                    PlastLevel = dto.PlastLevel,
                    DateAchieved = dto.DateAchieved
                });
            }
            else
            {
                var existingHistory = plastLevelHistory
                    .FirstOrDefault(e => e.PlastLevelHistoryKey == dto.PlastLevelHistoryKey);

                if (existingHistory != null)
                {
                    existingHistory.PlastLevel = dto.PlastLevel;
                    existingHistory.DateAchieved = dto.DateAchieved;
                }
            }
        }
    }

    private static bool TryNormalizeParentPhone(string? requested, string? stored, out string? phone)
    {
        phone = requested;
        if (string.IsNullOrWhiteSpace(requested) || string.Equals(requested, stored, StringComparison.Ordinal))
        {
            return true;
        }

        phone = PhoneNumbers.Normalize(requested);
        return phone is not null;
    }

    private bool HasSignificantProfileChange(
        UpsertMemberProfileCommand request,
        MemberEntity existing,
        MembershipEntity? current,
        Guid targetKurinKey,
        Guid? targetGroupKey)
    {
        return !string.Equals(existing.FirstName, request.FirstName, StringComparison.Ordinal)
               || !string.Equals(existing.MiddleName ?? string.Empty, request.MiddleName ?? string.Empty, StringComparison.Ordinal)
               || !string.Equals(existing.LastName, request.LastName, StringComparison.Ordinal)
               || !string.Equals(existing.Email, request.Email, StringComparison.OrdinalIgnoreCase)
               || !string.Equals(existing.PhoneNumber, request.PhoneNumber, StringComparison.Ordinal)
               || existing.DateOfBirth != request.DateOfBirth
               || !string.Equals(existing.Address ?? string.Empty, request.Address ?? string.Empty, StringComparison.Ordinal)
               || !string.Equals(existing.School ?? string.Empty, request.School ?? string.Empty, StringComparison.Ordinal)
               || existing.ParentsDiffer(request)
               || current?.GroupKey != targetGroupKey
               || current?.KurinKey != targetKurinKey
               || (CanEditPlastLevels(existing) && HasPlastLevelHistoryChange(request.PlastLevelHistories, existing.PlastLevelHistory));
    }

    private static bool HasPlastLevelHistoryChange(
        ICollection<PlastLevelHistoryDto> requested,
        ICollection<PlastLevelHistory> existing)
    {
        if (requested.Count != existing.Count)
        {
            return true;
        }

        var existingByKey = existing.ToDictionary(history => history.PlastLevelHistoryKey);
        foreach (var dto in requested)
        {
            if (!dto.PlastLevelHistoryKey.HasValue || dto.PlastLevelHistoryKey == Guid.Empty)
            {
                return true;
            }

            if (!existingByKey.TryGetValue(dto.PlastLevelHistoryKey.Value, out var entity))
            {
                return true;
            }

            if (entity.PlastLevel != dto.PlastLevel || entity.DateAchieved != dto.DateAchieved)
            {
                return true;
            }
        }

        return false;
    }
}
