using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <inheritdoc />
public sealed class MemberProgressDirectory : IMemberProgressDirectory
{
    private readonly IUnitOfWork _unitOfWork;

    public MemberProgressDirectory(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<KurinProgressFacts> GetFactsForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var badges = await _unitOfWork.BadgeProgresses.GetByKurinKeyAsync(kurinKey, cancellationToken);
        var points = await _unitOfWork.ProbePointProgresses.GetSignedByKurinKeyAsync(kurinKey, cancellationToken);
        var probes = await _unitOfWork.ProbeProgresses.GetByKurinKeyAsync(kurinKey, cancellationToken);

        return new KurinProgressFacts(
            [.. badges
                .Where(b => b.Status == BadgeProgressStatus.Confirmed && b.ReviewedAtUtc.HasValue)
                .Select(b => new BadgeConfirmedRecord(b.MemberKey, b.BadgeId, b.ReviewedAtUtc!.Value))],
            [.. points
                .Where(p => p.SignedAtUtc.HasValue)
                .Select(p => new ProbePointSignedRecord(p.MemberKey, p.ProbeId, p.PointId, p.SignedAtUtc!.Value))],
            [.. probes
                .Where(p => p.Status == ProbeProgressStatus.Verified && p.VerifiedAtUtc.HasValue)
                .Select(p => new ProbeClosedRecord(p.MemberKey, p.ProbeId, p.VerifiedAtUtc!.Value))]);
    }

    public async Task<IReadOnlyDictionary<Guid, MemberProgressDetail>> GetDetailsForMembersAsync(
        IReadOnlyCollection<Guid> memberKeys,
        CancellationToken cancellationToken = default)
    {
        if (memberKeys.Count == 0)
        {
            return new Dictionary<Guid, MemberProgressDetail>();
        }

        var probes = await _unitOfWork.ProbeProgresses.GetByMemberKeysAsync(memberKeys, cancellationToken);
        var points = await _unitOfWork.ProbePointProgresses.GetByMemberKeysAsync(memberKeys, cancellationToken);
        var badges = await _unitOfWork.BadgeProgresses.GetByMemberKeysAsync(memberKeys, cancellationToken);

        var probesByMember = probes
            .GroupBy(p => p.MemberKey)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ProbeProgressDetail>)[.. g.Select(p => new ProbeProgressDetail(
                p.ProbeId, p.Status, p.CompletedAtUtc, p.CompletedByName, p.VerifiedAtUtc, p.VerifiedByName))]);
        var pointsByMember = points
            .Where(p => p.IsSigned)
            .GroupBy(p => p.MemberKey)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ProbePointSignedDetail>)[.. g.Select(p => new ProbePointSignedDetail(
                p.ProbeId, p.PointId, p.SignedAtUtc, p.SignedByName, p.SignedByRole))]);
        var badgesByMember = badges
            .Where(b => b.Status == BadgeProgressStatus.Confirmed)
            .GroupBy(b => b.MemberKey)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<BadgeConfirmedDetail>)[.. g.Select(b => new BadgeConfirmedDetail(
                b.BadgeId, b.Status, b.ReviewedAtUtc, b.ReviewedByName, b.ReviewedByRole))]);

        return memberKeys
            .Distinct()
            .ToDictionary(key => key, key => new MemberProgressDetail(
                probesByMember.GetValueOrDefault(key) ?? [],
                pointsByMember.GetValueOrDefault(key) ?? [],
                badgesByMember.GetValueOrDefault(key) ?? []));
    }

    public async Task<MemberProgress> GetForMemberAsync(
        Guid memberKey,
        CancellationToken cancellationToken = default)
    {
        var probes = await _unitOfWork.ProbeProgresses.GetByMemberKeyAsync(memberKey, cancellationToken);
        var badges = await _unitOfWork.BadgeProgresses.GetByMemberKeyAsync(memberKey, cancellationToken);

        return new MemberProgress(
            [.. probes
                .OrderBy(progress => progress.ProbeId)
                .Select(progress => new ProbeProgressRecord(
                    progress.ProbeId,
                    progress.Status,
                    progress.KurinKey,
                    progress.CompletedAtUtc,
                    progress.VerifiedAtUtc))],
            [.. badges
                .OrderBy(progress => progress.BadgeId)
                .Select(progress => new BadgeProgressRecord(
                    progress.BadgeId,
                    progress.Status,
                    progress.KurinKey,
                    progress.SubmittedAtUtc,
                    progress.ReviewedAtUtc))]);
    }

    public async Task<int> CountSubmittedBadgesAsync(IReadOnlyCollection<Guid> memberKeys, CancellationToken cancellationToken = default)
        => memberKeys.Count == 0
            ? 0
            : (await _unitOfWork.BadgeProgresses.GetByMemberKeysAsync(memberKeys, cancellationToken)).Count(b => b.Status == BadgeProgressStatus.Submitted);

    public async Task<IReadOnlyCollection<string>> GetSignedPointIdsAsync(Guid memberKey, string probeId, CancellationToken cancellationToken = default)
        => [.. (await _unitOfWork.ProbePointProgresses.GetByMemberAndProbeAsync(memberKey, probeId, cancellationToken))
            .Where(p => p.IsSigned)
            .Select(p => p.PointId)];

    public async Task<int> CountForMemberAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => await _unitOfWork.ProbeProgresses.CountByMemberKeyAsync(memberKey, cancellationToken)
           + await _unitOfWork.BadgeProgresses.CountByMemberKeyAsync(memberKey, cancellationToken);
}
