using ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Records.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Services;

/// <inheritdoc />
public sealed class MemberDirectory : IMemberDirectory
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly IBlobReadLinks _links;

    public MemberDirectory(IMemberUnitOfWork unitOfWork, IBlobReadLinks links)
    {
        _unitOfWork = unitOfWork;
        _links = links;
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

    public async Task<IReadOnlyDictionary<Guid, MemberLookupDto>> GetLookupsAsync(
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default)
        => memberKeys.Count == 0
            ? new Dictionary<Guid, MemberLookupDto>()
            : (await _unitOfWork.Members.GetManyAsync(memberKeys, cancellationToken))
                .ToDictionary(member => member.MemberKey, member => member.ToLookupDto(_links));

    public Task<IReadOnlyCollection<MemberIdentity>> FindPossibleMatchesAsync(
        IReadOnlyCollection<string> emails,
        IReadOnlyCollection<string> phoneNumbers,
        IReadOnlyCollection<string> lastNames,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Members.FindPossibleMatchesAsync(emails, phoneNumbers, lastNames, cancellationToken);

    public Task<IReadOnlyCollection<MemberSummary>> GetAllAsync(CancellationToken cancellationToken = default)
        => _unitOfWork.Members.GetAllSummariesAsync(cancellationToken);
}
