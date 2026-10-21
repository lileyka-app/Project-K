using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <inheritdoc />
public sealed class MemberDirectory : IMemberDirectory
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly IDomainEventPublisher _events;

    public MemberDirectory(IMemberUnitOfWork unitOfWork, IDomainEventPublisher events)
    {
        _unitOfWork = unitOfWork;
        _events = events;
    }

    public Task<bool> ExistsAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => _unitOfWork.Members.ExistsAsync(memberKey, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _unitOfWork.Members.ExistsByEmailAsync(email, cancellationToken);

    public Task<MemberCard?> FindByPublicIdAsync(string publicId, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(publicId)
            ? Task.FromResult<MemberCard?>(null)
            : _unitOfWork.Members.GetCardByPublicIdAsync(
                publicId.Trim().ToUpperInvariant(), cancellationToken);

    public Task<MemberSummary?> FindAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => _unitOfWork.Members.GetSummaryByKeyAsync(memberKey, cancellationToken);

    public Task<MemberSummary?> FindByAccountAsync(Guid userKey, CancellationToken cancellationToken = default)
        => _unitOfWork.Members.GetSummaryByUserKeyAsync(userKey, cancellationToken);

    public Task<Guid?> FindAccountKeyAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => _unitOfWork.Members.GetUserKeyByMemberAsync(memberKey, cancellationToken);

    public Task<Guid?> FindKurinKeyAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => _unitOfWork.Members.GetKurinKeyByMemberAsync(memberKey, cancellationToken);

    public async Task<IReadOnlyCollection<PlastLevel>> GetLevelsAsync(Guid memberKey, CancellationToken cancellationToken = default)
    {
        var member = await _unitOfWork.Members.GetByKeyAsync(memberKey, cancellationToken);
        return member?.PlastLevelHistory.Select(h => h.PlastLevel).ToList() ?? [];
    }

    public Task<IReadOnlyCollection<MemberSummary>> GetByKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Members.GetSummariesByKurinKeyAsync(kurinKey, cancellationToken);

    public Task<IReadOnlyCollection<MemberSummary>> GetByKurinAsync(
        Guid kurinKey,
        MemberSelection selection,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Members.GetSummariesByKurinKeyAsync(kurinKey, selection, cancellationToken);

    public Task<IReadOnlyCollection<MemberSummary>> GetByKeysAsync(
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Members.GetSummariesByKeysAsync(memberKeys, cancellationToken);

    public async Task<IReadOnlyCollection<WarningRecord>> GetActiveWarningsInKurinAsync(
        Guid kurinKey,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
        => (await _unitOfWork.MemberWarnings.GetActiveByKurinKeyAsync(kurinKey, nowUtc, cancellationToken))
            .Select(w => new WarningRecord(w.MemberKey, w.Level, w.IssuedAtUtc))
            .ToList();

    public async Task<IReadOnlyCollection<MemberLookupDto>> GetLookupByKurinAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default)
        => (await _unitOfWork.Members.GetMentorCandidatesLookupAsync(kurinKey, cancellationToken)).ToList();

    public Task<IReadOnlyCollection<MemberSummary>> GetAllAsync(CancellationToken cancellationToken = default)
        => _unitOfWork.Members.GetAllSummariesAsync(cancellationToken);

    public async Task<Guid> EnsureForAccountAsync(
        MemberForAccount details,
        CancellationToken cancellationToken = default)
    {
        var existing = await _unitOfWork.Members.GetByEmailAsync(details.Email, cancellationToken);
        if (existing is not null)
        {
            existing.UserKey = details.UserKey;
            _unitOfWork.Members.Update(existing, cancellationToken);
            await _events.PublishAsync(
                new MemberAccountLinked(existing.MemberKey, details.UserKey),
                cancellationToken);
            return existing.MemberKey;
        }

        var member = new Member
        {
            MemberKey = Guid.NewGuid(),
            FirstName = details.FirstName,
            LastName = details.LastName,
            Email = details.Email,
            PhoneNumber = details.PhoneNumber,
            DateOfBirth = details.DateOfBirth,
            UserKey = details.UserKey
        };

        _unitOfWork.Members.Create(member, cancellationToken);
        await _events.PublishAsync(
            new MemberAccountLinked(member.MemberKey, details.UserKey),
            cancellationToken);

        // An account can arrive without a kurin behind it, and then there is no membership to open —
        // the member record cannot name a kurin either, and that is where it fails.
        if (details.KurinKey != Guid.Empty)
        {
            await _events.PublishAsync(
                new MemberPlaced(member.MemberKey, details.UserKey, details.KurinKey, null),
                cancellationToken);
        }

        return member.MemberKey;
    }

    public async Task SetEmailFromAccountAsync(
        Guid userKey,
        string email,
        CancellationToken cancellationToken = default)
    {
        var member = await _unitOfWork.Members.GetTrackedByUserKeyAsync(userKey, cancellationToken);
        if (member is null)
        {
            return;
        }

        member.Email = email;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPhoneFromAccountAsync(
        Guid userKey,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var member = await _unitOfWork.Members.GetTrackedByUserKeyAsync(userKey, cancellationToken);
        if (member is null)
        {
            return;
        }

        member.PhoneNumber = phoneNumber;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RemoveAsync(Guid memberKey, CancellationToken cancellationToken = default)
    {
        var member = await _unitOfWork.Members.GetByKeyAsync(memberKey, cancellationToken);
        if (member is null)
        {
            return false;
        }

        _unitOfWork.Members.Delete(member, cancellationToken);
        await AnnounceRemovalAsync([memberKey], cancellationToken);
        return true;
    }

    private Task AnnounceRemovalAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken)
        => memberKeys.Count == 0
            ? Task.CompletedTask
            : _events.PublishAsync(new MembersRemoved(memberKeys), cancellationToken);
}
