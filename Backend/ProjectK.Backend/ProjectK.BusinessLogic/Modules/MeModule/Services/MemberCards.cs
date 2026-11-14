using Microsoft.Extensions.Options;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MeModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Options.InfrastructureModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.KurinModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.MeModule.Services;

/// <summary>
/// Shared member cards: who may share one, and what it shows. Every read decides both again, so a
/// kurin that withdraws its permission, or a person who hides a part, takes effect on the next scan.
/// </summary>
public sealed class MemberCards
{
    /// <summary>How long a «перевірити» QR works. A screenshot of it is useless soon after.</summary>
    public static readonly TimeSpan CheckLifetime = TimeSpan.FromMinutes(3);

    public const string CheckPath = "/v/";
    public const string LinkPath = "/c/";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly IMemberProgressDirectory _progress;
    private readonly IProbesCatalogService _probes;
    private readonly IBadgesCatalogService _badges;
    private readonly IBlobReadLinks _links;
    private readonly IResourceScopeReader _scope;
    private readonly IHonoursCatalog _honours;
    private readonly EmailOptions _site;
    private readonly TimeProvider _time;

    public MemberCards(
        IUnitOfWork unitOfWork,
        IMemberDirectory members,
        IMemberProgressDirectory progress,
        IProbesCatalogService probes,
        IBadgesCatalogService badges,
        IBlobReadLinks links,
        IResourceScopeReader scope,
        IHonoursCatalog honours,
        IOptions<EmailOptions> site,
        TimeProvider time)
    {
        _scope = scope;
        _honours = honours;
        _time = time;
        _unitOfWork = unitOfWork;
        _members = members;
        _progress = progress;
        _probes = probes;
        _badges = badges;
        _links = links;
        _site = site.Value;
    }

    /// <summary>The site's public address followed by <paramref name="path"/>.</summary>
    public string SiteUrl(string path) => $"{_site.BaseUrl.TrimEnd('/')}{path}";

    public async Task<CardAllowanceDto> AllowanceAsync(Guid memberKey, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        var born = await _members.GetDateOfBirthAsync(memberKey, cancellationToken);
        if (born is { } date && date.AddYears(18) <= today)
        {
            return new CardAllowanceDto(MayShare: true, IsAdult: true, CardField.All, MayLink: true);
        }

        var kurinKeys = (await _unitOfWork.Memberships.GetActiveForMemberAsync(memberKey, cancellationToken))
            .Select(m => m.KurinKey)
            .Distinct()
            .ToList();
        var settings = kurinKeys.Count == 0
            ? []
            : await _unitOfWork.KurinCardSharing.GetForKurinsAsync(kurinKeys, cancellationToken);

        // Every kurin the person stands in has to allow it: one that never said yes has not said yes.
        if (kurinKeys.Count == 0 || settings.Count < kurinKeys.Count || settings.Any(s => !s.MinorsMayShare))
        {
            return new CardAllowanceDto(MayShare: false, IsAdult: false, CardField.None, MayLink: false);
        }

        var fields = settings.Aggregate(CardField.All, (all, s) => all & s.MinorFields);
        return new CardAllowanceDto(MayShare: true, IsAdult: false, fields, MayLink: settings.All(s => s.MinorsMayLink));
    }

    /// <summary>The fields the person chose, cut to what they are allowed.</summary>
    public async Task<CardField> ChosenFieldsAsync(Guid memberKey, CardAllowanceDto allowance, CancellationToken cancellationToken)
    {
        var share = await _unitOfWork.MemberCardShares.GetForMemberAsync(memberKey, cancellationToken);
        return (share?.Fields ?? MemberCardShare.DefaultFields) & allowance.MaxFields;
    }

    /// <summary>The person's row, made (and left for the caller to save) when they have none yet.</summary>
    public async Task<MemberCardShare> ShareOfAsync(Guid memberKey, CancellationToken cancellationToken)
    {
        if (await _unitOfWork.MemberCardShares.GetForMemberAsync(memberKey, cancellationToken) is { } existing)
        {
            return existing;
        }

        var share = new MemberCardShare { MemberKey = memberKey };
        _unitOfWork.MemberCardShares.Create(share);
        return share;
    }

    /// <summary>The card as a scan or a link shows it: what was chosen, cut to what is allowed.</summary>
    public async Task<ServiceResult<PublicCardDto>> SharedAsync(Guid memberKey, CardAllowanceDto allowance, DateTime? checkedAtUtc, CancellationToken cancellationToken)
    {
        var fields = await ChosenFieldsAsync(memberKey, allowance, cancellationToken);
        var card = await ReadAsync(memberKey, fields, checkedAtUtc, cancellationToken);
        return card is null
            ? MeErrors.NoCard<PublicCardDto>()
            : new ServiceResult<PublicCardDto>(ResultType.Success, card);
    }

    /// <summary>The person's own view: what they may do, what they chose, the link and the card as others see it.</summary>
    public async Task<MyCardDto> MineAsync(Guid memberKey, CancellationToken cancellationToken)
    {
        var allowance = await AllowanceAsync(memberKey, cancellationToken);
        var share = await _unitOfWork.MemberCardShares.GetForMemberAsync(memberKey, cancellationToken);
        var chosen = share?.Fields ?? MemberCardShare.DefaultFields;
        var preview = await ReadAsync(memberKey, chosen & allowance.MaxFields, null, cancellationToken);
        var link = allowance.MayLink && share?.LinkToken is { } token ? SiteUrl(LinkPath + token) : null;

        return new MyCardDto(allowance, chosen, link, link is null ? null : share!.LinkIssuedAtUtc, preview!);
    }

    /// <summary>The card with just <paramref name="fields"/>; null when the person does not exist.</summary>
    public async Task<PublicCardDto?> ReadAsync(Guid memberKey, CardField fields, DateTime? checkedAtUtc, CancellationToken cancellationToken)
    {
        var person = await _members.FindAsync(memberKey, cancellationToken);
        if (person is null)
        {
            return null;
        }

        IReadOnlyList<PublicCardKurinDto>? kurins = null;
        if (fields.HasFlag(CardField.Kurin))
        {
            kurins = (await _unitOfWork.Memberships.GetRecordsForMemberAsync(memberKey, cancellationToken))
                .Where(m => m.IsCurrent)
                .OrderBy(m => m.KurinNumber)
                .Select(m => new PublicCardKurinDto(m.KurinNumber, m.KurinNamedAfter, m.GroupName, m.Numbering))
                .ToList();
        }

        string? level = null;
        if (fields.HasFlag(CardField.Level)
            && PlastLadder.Highest(await _members.GetLevelsAsync(memberKey, cancellationToken)) is { } highest)
        {
            level = PlastLevelNames.Of(highest);
        }

        IReadOnlyList<PublicCardAwardDto>? awards = null;
        if (fields.HasFlag(CardField.Awards))
        {
            // By default the line the person stands in now: a УСП or УПС member shows the senior
            // відзначення, a юнак the УПЮ ones. The whole way, every line marked, only when they chose it.
            var whole = fields.HasFlag(CardField.AwardsHistory);
            var senior = !whole && await _scope.StandsInSeniorKurinAsync(ResourceType.Member, memberKey, cancellationToken);
            awards = (await _members.GetConfirmedAwardsAsync(memberKey, cancellationToken))
                .Where(a => whole || (senior ? a.CatalogId is not null : a.Level is not null))
                .OrderByDescending(a => a.DateAcquired)
                .Select(ToPublic)
                .ToList();
        }

        IReadOnlyList<PublicCardBadgeDto>? badges = null;
        IReadOnlyList<PublicCardProbeDto>? probes = null;
        if (fields.HasFlag(CardField.Badges) || fields.HasFlag(CardField.Probes))
        {
            var progress = await _progress.GetForMemberAsync(memberKey, cancellationToken);
            if (fields.HasFlag(CardField.Badges))
            {
                badges = progress.Badges
                    .Where(b => b.Status == BadgeProgressStatus.Confirmed)
                    .OrderByDescending(b => b.ReviewedAtUtc)
                    .Select(b => _badges.GetBadgeById(b.BadgeId) is { } badge
                        ? new PublicCardBadgeDto(badge.Title, badge.ImagePath, b.ReviewedAtUtc)
                        : new PublicCardBadgeDto(b.BadgeId, null, b.ReviewedAtUtc))
                    .ToList();
            }

            if (fields.HasFlag(CardField.Probes))
            {
                probes = progress.Probes
                    .Where(p => p.Status == ProbeProgressStatus.Verified)
                    .OrderBy(p => p.VerifiedAtUtc)
                    .Select(p => new PublicCardProbeDto(_probes.GetGroupedProbeById(p.ProbeId)?.Title ?? p.ProbeId, p.VerifiedAtUtc))
                    .ToList();
            }
        }

        return new PublicCardDto(
            person.FullName,
            fields.HasFlag(CardField.Photo) ? _links.ForSharing(person.ProfilePhotoBlobName) : null,
            kurins,
            level,
            awards,
            badges,
            probes,
            checkedAtUtc);
    }

    private PublicCardAwardDto ToPublic(ConfirmedAward award)
    {
        if (award.Level is { } level)
        {
            return new PublicCardAwardDto((int)level, KurinReportTerminology.AwardLevel(level), null, award.DateAcquired, "UPYu");
        }

        var item = _honours.GetById(award.CatalogId!);
        return new PublicCardAwardDto(null, item?.Title ?? award.CatalogId!, item?.ImageUrl, award.DateAcquired, UladOf(item?.Ulad));
    }

    /// <summary>The catalogue's ulad code as the card names it; a senior honour of both lines has none.</summary>
    private static string? UladOf(string? ulad) => ulad?.Trim().ToLowerInvariant() switch
    {
        "usp" => "USP",
        "ups" => "UPS",
        "upyu" => "UPYu",
        _ => null
    };
}
