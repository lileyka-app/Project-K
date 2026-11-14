using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <inheritdoc />
public sealed class MemberDirectory : IMemberDirectory
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly ISender _sender;

    public MemberDirectory(IMemberUnitOfWork unitOfWork, ISender sender)
    {
        _unitOfWork = unitOfWork;
        _sender = sender;
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

    public async Task<string?> GetPublicIdAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => (await _unitOfWork.Members.GetByKeyAsync(memberKey, cancellationToken))?.PublicId;

    public async Task<DateOnly?> GetDateOfBirthAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => (await _unitOfWork.Members.GetByKeyAsync(memberKey, cancellationToken))?.DateOfBirth is { } born && born != default ? born : null;

    public async Task<IReadOnlyList<ConfirmedAward>> GetConfirmedAwardsAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => (await _unitOfWork.MemberAwards.GetByMemberKeyAsync(memberKey, cancellationToken))
            .Where(a => a.Status == BadgeProgressStatus.Confirmed)
            .OrderByDescending(a => a.DateAcquired)
            .Select(a => new ConfirmedAward(a.Level, a.CatalogId, a.DateAcquired))
            .ToList();

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

    /// <inheritdoc />
    /// <remarks>
    /// Sent through <see cref="GetMembersQuery"/> rather than read here, so the masking that use case
    /// applies is applied once, in one place, for the screen and the file alike.
    /// </remarks>
    public async Task<IReadOnlyList<MemberDto>> GetVisibleRosterAsync(
        Guid kurinKey,
        CancellationToken cancellationToken = default)
        => (await _sender.Send(new GetMembersQuery(Guid.Empty, kurinKey), cancellationToken)).Data?.ToList() ?? [];

    public Task<IReadOnlyCollection<MemberIdentity>> FindPossibleMatchesAsync(
        IReadOnlyCollection<string> emails,
        IReadOnlyCollection<string> phoneNumbers,
        IReadOnlyCollection<string> lastNames,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Members.FindPossibleMatchesAsync(emails, phoneNumbers, lastNames, cancellationToken);

    public Task<IReadOnlyCollection<MemberSummary>> GetAllAsync(CancellationToken cancellationToken = default)
        => _unitOfWork.Members.GetAllSummariesAsync(cancellationToken);
}
