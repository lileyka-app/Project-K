using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Models;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Krai;

/// <summary>
/// For one quarter: how many people in the край's kurins pay less thanks to each of its пільги, and
/// how much each part of the вкладка came out lower than without them. Only a пільга that set
/// someone's price counts for them; one outdone by a cheaper пільга does not.
/// </summary>
/// <remarks>Without a quarter it reads the one running now by the край's own clock.</remarks>
public sealed record GetKraiConcessionReportQuery(string KraiCode, int? Year = null, int? Number = null)
    : IRequest<ServiceResult<KraiConcessionReportResponse>>;

public sealed class GetKraiConcessionReportQueryHandler
    : IRequestHandler<GetKraiConcessionReportQuery, ServiceResult<KraiConcessionReportResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly DuesLedgerReader _ledgers;
    private readonly IKraiProfileProvider _profiles;
    private readonly TimeProvider _time;

    public GetKraiConcessionReportQueryHandler(IUnitOfWork unitOfWork, DuesLedgerReader ledgers, IKraiProfileProvider profiles, TimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _ledgers = ledgers;
        _profiles = profiles;
        _time = time;
    }

    public async Task<ServiceResult<KraiConcessionReportResponse>> Handle(GetKraiConcessionReportQuery request, CancellationToken cancellationToken)
    {
        if ((request.Year is null) != (request.Number is null) || request.Number is < 1 or > 4 || request.Year is < 2000 or > 2100)
        {
            return ServiceResult<KraiConcessionReportResponse>.Failure(ResultType.BadRequest, "InvalidQuarter", "Квартал задано неправильно.");
        }

        var code = request.KraiCode.Trim().ToLowerInvariant();
        var profile = await _profiles.GetForKraiAsync(code, cancellationToken);
        if (profile is null)
        {
            return ServiceResult<KraiConcessionReportResponse>.Failure(ResultType.NotFound, "KraiNotFound", "Krai not found.");
        }

        var quarter = request is { Year: int year, Number: int number }
            ? new DuesQuarter(year, number)
            : profile.ToDuesKrai().QuarterOf(_time.GetUtcNow());

        // Each ledger is opened in turn: they share one DbContext, and opening one brings its
        // charges up to the running quarter, as every read of the вкладка does.
        var kurins = await _unitOfWork.Kurins.GetInKraiAsync(code, cancellationToken);

        var rows = new Dictionary<string, ReportRow>(StringComparer.Ordinal);
        foreach (var kurin in kurins)
        {
            var ledger = await _ledgers.OpenAsync(kurin.KurinKey, cancellationToken);
            foreach (var account in ledger.Accounts())
            {
                if (!account.Quarters.Any(q => q.Quarter == quarter))
                {
                    continue;
                }

                var price = ledger.Priced(account.MembershipKey, account.GroupKey, quarter);
                if (price.Grant is not { } grant)
                {
                    continue;
                }

                if (!rows.TryGetValue(grant.ConcessionKey, out var row))
                {
                    row = new ReportRow(grant.ConcessionKey);
                    rows[grant.ConcessionKey] = row;
                }

                row.Name ??= grant.Name;
                row.Add(kurin.KurinKey, kurin.Number, account.MembershipKey, price.WithoutGrants, price.Amount);
            }
        }

        // The край's current names win over a name copied onto an older grant.
        foreach (var rule in profile.Dues.Concessions)
        {
            if (rows.TryGetValue(rule.Key, out var row))
            {
                row.Name = rule.Name;
            }
        }

        return new ServiceResult<KraiConcessionReportResponse>(ResultType.Success, new KraiConcessionReportResponse
        {
            KraiCode = profile.KraiCode,
            KraiName = profile.KraiName,
            Currency = profile.Locale.Currency,
            NumberLocale = profile.Locale.NumberLocale,
            Quarter = new QuarterDto { Year = quarter.Year, Number = quarter.Number },
            KurinCount = kurins.Count,
            Concessions = rows.Values
                .OrderBy(r => r.Name, StringComparer.CurrentCulture)
                .Select(r => r.ToDto())
                .ToList()
        });
    }

    private sealed class ReportRow(string key)
    {
        private readonly HashSet<Guid> _people = [];
        private readonly Dictionary<Guid, (int? Number, HashSet<Guid> People, decimal Off)> _kurins = [];
        private decimal _stanytsia, _krai, _kurin, _group;

        public string Key { get; } = key;
        public string? Name { get; set; }

        public void Add(Guid kurinKey, int? number, Guid membershipKey, DuesAmount without, DuesAmount with)
        {
            _people.Add(membershipKey);
            _stanytsia += without.Stanytsia - with.Stanytsia;
            _krai += without.Krai - with.Krai;
            _kurin += without.Kurin - with.Kurin;
            _group += without.Group - with.Group;

            if (!_kurins.TryGetValue(kurinKey, out var kurin))
            {
                kurin = (number, [], 0);
            }

            kurin.People.Add(membershipKey);
            _kurins[kurinKey] = (kurin.Number, kurin.People, kurin.Off + without.Total - with.Total);
        }

        public KraiConcessionReportRowDto ToDto() => new()
        {
            Key = Key,
            Name = Name ?? Key,
            People = _people.Count,
            Reduction = new DuesAmountDto
            {
                Stanytsia = _stanytsia,
                Krai = _krai,
                Kurin = _kurin,
                Group = _group,
                Total = _stanytsia + _krai + _kurin + _group
            },
            Kurins = _kurins
                .OrderBy(k => k.Value.Number ?? int.MaxValue)
                .Select(k => new KraiConcessionReportKurinDto
                {
                    KurinKey = k.Key,
                    Number = k.Value.Number,
                    People = k.Value.People.Count,
                    Reduction = k.Value.Off
                })
                .ToList()
        };
    }
}
