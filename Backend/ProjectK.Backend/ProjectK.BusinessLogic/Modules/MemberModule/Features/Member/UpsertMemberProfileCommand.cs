using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.MemberModule;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.AuthModule;
using ProjectK.Common.Rules.MemberModule;
using ProjectK.Common.Rules.Shared;
using GroupEntity = ProjectK.Common.Entities.KurinModule.Group;
using MemberEntity = ProjectK.Common.Entities.MemberModule.Member;
using MembershipEntity = ProjectK.Common.Entities.MemberModule.Membership;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;

/// <summary>
/// The member's own data — who they are and where they are placed. Carries no photo and no
/// account: those are separate use cases, so this one owns exactly the member row.
/// </summary>
public class UpsertMemberProfileCommand : IRequest<ServiceResult<MemberProfileWriteResult>>, IHasParentContacts
{
    public Guid MemberKey { get; set; }
    public Guid? KurinKey { get; set; }
    public Guid? GroupKey { get; set; }
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

    /// <summary>Values of the край's own card fields, by key. A key left out keeps its value.</summary>
    public IDictionary<string, string>? CardExtras { get; set; }

    /// <summary>
    /// Holds the write to the card fields of the member's край: required ones filled, hidden ones
    /// left as they are. The forms ask for it; the roster import and kurin registration do not, so
    /// a край's new required field does not turn away a whole import.
    /// </summary>
    public bool ApplyCardFields { get; set; }
}

public class UpsertMemberProfileCommandHandler
    : IRequestHandler<UpsertMemberProfileCommand, ServiceResult<MemberProfileWriteResult>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    // Placement is the kurin's to record, so writing a member still means asking the kurin
    // which гурток is meant and telling it where the person now stands.
    private readonly IUnitOfWork _kurinData;
    private readonly IMembershipRepository _memberships;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IDomainEventPublisher _events;
    private readonly IAccountProvisioningService _accounts;
    private readonly IKraiProfileDirectory _profiles;
    private readonly IMemberFieldAccess _fieldAccess;
    private readonly IKurinAudit _audit;
    private readonly IResourceAccessService _access;

    public UpsertMemberProfileCommandHandler(
        IMemberUnitOfWork unitOfWork,
        IUnitOfWork kurinData,
        ICurrentUserContext currentUserContext,
        IDomainEventPublisher events,
        IAccountProvisioningService accounts,
        IKraiProfileDirectory profiles,
        IMemberFieldAccess fieldAccess,
        IKurinAudit audit,
        IResourceAccessService access)
    {
        _access = access;
        _fieldAccess = fieldAccess;
        _audit = audit;
        _accounts = accounts;
        _profiles = profiles;
        _unitOfWork = unitOfWork;
        _kurinData = kurinData;
        _memberships = kurinData.Memberships;
        _currentUserContext = currentUserContext;
        _events = events;
    }

    public async Task<ServiceResult<MemberProfileWriteResult>> Handle(
        UpsertMemberProfileCommand request,
        CancellationToken cancellationToken)
    {
        var existing = await _unitOfWork.Members.GetByKeyAsync(request.MemberKey, cancellationToken);
        var before = existing is null ? null : MemberTrail.Of(existing);

        // Where they already stand. The record does not say any more, so it is read from the
        // membership — and it is what the request is measured against. Someone in two kurins is
        // measured by the one the edit is made from.
        var standing = existing is null
            ? []
            : await _memberships.GetActiveForMemberAsync(request.MemberKey, cancellationToken);
        var current = standing.FirstOrDefault(m => m.KurinKey == _currentUserContext.KurinKey)
            ?? standing.FirstOrDefault();

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
            HiddenDataOnWriteRules.KeepStored(hiddenFromWriter, request, existing);
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return ServiceResult<MemberProfileWriteResult>.Failure(ResultType.BadRequest, MemberErrors.EmailRequired, "An email is required.");
        }

        GroupEntity? group = null;
        if (request.GroupKey.HasValue && request.GroupKey.Value != Guid.Empty)
        {
            group = await _kurinData.Groups.GetByKeyAsync(request.GroupKey.Value, cancellationToken);
        }

        if (group == null && (!request.KurinKey.HasValue || request.KurinKey.Value == Guid.Empty))
        {
            return ServiceResult<MemberProfileWriteResult>.Failure(ResultType.NotFound, MemberErrors.KurinNotFound, "There is no group or kurin to place the member in.");
        }

        // Where they are to stand once this is written.
        var kurinKey = group?.KurinKey ?? request.KurinKey!.Value;
        var groupKey = group?.GroupKey;

        var cardExtras = existing?.CardExtras;
        if (request.ApplyCardFields && await _profiles.GetForKurinAsync(kurinKey, cancellationToken) is { } profile)
        {
            var card = MemberCardFieldRules.Apply(profile.CardFields, request, existing, hiddenFromWriter);
            if (card.Error is not null)
            {
                return ServiceResult<MemberProfileWriteResult>.Failure(ResultType.BadRequest, MemberErrors.CardFieldRequired, card.Error);
            }

            cardExtras = card.CardExtras;
        }
        else if (existing is not null)
        {
            // A write that does not carry the parents' contacts (the roster import) keeps them.
            foreach (var field in ParentContactRules.Fields)
            {
                field.Set(request, field.Get(request) ?? field.Get(existing));
            }
        }

        // Every write stores the phone as +380501234567. One the member already has is let through
        // as it is, so a number older than the rule does not block an edit of something else.
        if (existing is null || !string.Equals(request.PhoneNumber, existing.PhoneNumber, StringComparison.Ordinal))
        {
            var phone = PhoneNumberRules.Normalize(request.PhoneNumber);
            if (phone is null)
            {
                return ServiceResult<MemberProfileWriteResult>.Failure(ResultType.BadRequest, PhoneNumberRules.ErrorCode, PhoneNumberRules.InvalidMessage);
            }

            request.PhoneNumber = phone;
        }

        // A parent's phone is stored in the same shape; an empty one stays empty.
        if (!TryNormalizeParentPhone(request.MotherPhone, existing?.MotherPhone, out var motherPhone)
            || !TryNormalizeParentPhone(request.FatherPhone, existing?.FatherPhone, out var fatherPhone))
        {
            return ServiceResult<MemberProfileWriteResult>.Failure(ResultType.BadRequest, PhoneNumberRules.ErrorCode, PhoneNumberRules.InvalidMessage);
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
            existing = new MemberEntity();
            request.ApplyTo(existing, DateTime.UtcNow);
            existing.CardExtras = cardExtras;

            // The ступені the request carried. ApplyTo deliberately leaves the collection
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

            // A senior's ступені: the person, or who holds the kurin-wide right where they stand now.
            // Refused before anything changes: skipped quietly, the save answered 200 and still
            // marked the profile stale over ступені it never wrote.
            if (CanEditPlastLevels(existing)
                && HasPlastLevelHistoryChange(request.PlastLevelHistories, existing.PlastLevelHistory)
                && await _access.CheckSeniorYouthRecordAsync(ResourceType.Member, ResourceAction.Update, existing.MemberKey, ownerMayChange: true, cancellationToken) is { IsAllowed: false } senior)
            {
                return ServiceResult<MemberProfileWriteResult>.Failure(ResultType.Forbidden, MemberErrors.SeniorYouthRecord, senior.Reason);
            }

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
                        MemberErrors.ContactInfoLinked,
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
                            MemberErrors.EmailTaken,
                            "This email is already used by another account or registration.");
                    }
                }

                preserveLinkedUserEmail = emailChanged && !accountEmailToFollow && !IsAdmin();
                linkedUserEmail = preserveLinkedUserEmail ? existing.Email : null;
            }

            previousPhotoBlobName = existing.ProfilePhotoBlobName;
            request.ApplyTo(existing, DateTime.UtcNow);
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

        await RecordAsync(existing, before, kurinKey, cancellationToken);

        // Where the person stands is the kurin's to record, and it is the only place it is
        // written. The member module says who was put where; the kurin opens the membership in
        // response, inside this same unit of work. Someone who already stands somewhere keeps
        // every membership as it is: an edit is not a move.
        if (current is null)
        {
            await _events.PublishAsync(
                new MemberPlaced(existing.MemberKey, existing.UserKey, kurinKey, groupKey),
                cancellationToken);
        }

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            return ServiceResult<MemberProfileWriteResult>.Failure(ResultType.InternalServerError, MemberErrors.MemberNotSaved, "The member could not be saved.");
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

    /// <summary>
    /// Records a new card with what it was made with, or an edit with what it changed; an edit that
    /// changed nothing is not recorded. A new person is not in any kurin yet, so the entry is filed
    /// under the one they are being placed in; an edit goes to the kurin the request acts in, or to
    /// where the person stands when it acts in none.
    /// </summary>
    private async Task RecordAsync(
        MemberEntity member,
        (string Field, object? Value)[]? before,
        Guid kurinKey,
        CancellationToken cancellationToken)
    {
        var changes = before is null
            ? MemberTrail.Added(member)
            : MemberTrail.Between(before, MemberTrail.Of(member));
        if (changes.Count == 0)
        {
            return;
        }

        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Member,
                before is null ? AuditActionNames.Member.Added : AuditActionNames.Member.Changed,
                member.MemberKey,
                MemberTrail.LabelOf(member),
                member.MemberKey,
                changes,
                KurinKey: before is null ? kurinKey : _currentUserContext.KurinKey ?? kurinKey),
            cancellationToken);
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
                    DateAchieved = dto.DateAchieved,
                    IsPlastunka = dto.IsPlastunka
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
                    existingHistory.IsPlastunka = dto.IsPlastunka;
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

        phone = PhoneNumberRules.Normalize(requested);
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
