using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.InfrastructureModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.KurinModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Registry;

/// <summary>
/// The kurin's roster as a spreadsheet, in the columns the caller asked for and in their order.
/// The screen decides what is shown; this only writes it down.
/// </summary>
/// <param name="ColumnIds">
/// Column ids from the registry screen. Unknown ids are dropped rather than failing the export —
/// a column removed from the app should not break a saved choice.
/// </param>
public sealed record ExportRegistryQuery(Guid KurinKey, IReadOnlyList<string> ColumnIds)
    : IRequest<ServiceResult<RegistryFile>>;

/// <summary>
/// Writes the roster the caller can already see, as the three sheets the screen shows: юнаки,
/// впорядники, and how many юнаки stand at each ступінь.
/// <para>
/// The people come from <see cref="GetMembersQuery"/> — the read the screen makes — rather than
/// from a read of its own, and that is the point: whatever the roster masks from the caller is
/// masked here too, without this handler knowing the rule. An export that read the table directly
/// would be a way around it.
/// </para>
/// </summary>
public sealed class ExportRegistryQueryHandler : IRequestHandler<ExportRegistryQuery, ServiceResult<RegistryFile>>
{
    private readonly ISender _sender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISpreadsheetWriter _spreadsheet;
    private readonly TimeProvider _timeProvider;

    public ExportRegistryQueryHandler(
        ISender sender,
        IUnitOfWork unitOfWork,
        ISpreadsheetWriter spreadsheet,
        TimeProvider timeProvider)
    {
        _sender = sender;
        _unitOfWork = unitOfWork;
        _spreadsheet = spreadsheet;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<RegistryFile>> Handle(
        ExportRegistryQuery request,
        CancellationToken cancellationToken)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return KurinErrors.NotFound<RegistryFile>();
        }

        var members = (await _sender.Send(new GetMembersQuery(Guid.Empty, request.KurinKey), cancellationToken)).Data?.ToList() ?? [];

        var chosen = request.ColumnIds
            .Where(id => RegistryColumnRules.HeaderFor(id) is not null)
            .ToList();

        var people = members
            .OrderBy(member => $"{member.LastName} {member.FirstName}", StringComparer.CurrentCulture)
            .ToList();

        // The people carry only the tags the caller may see; these are just their names.
        var tagNames = (await _unitOfWork.KurinTags.GetForKurinAsync(request.KurinKey, cancellationToken))
            .ToDictionary(tag => tag.KurinTagKey, tag => tag.Name);

        var youth = people.Where(member => !member.IsStaff).ToList();
        var staff = people.Where(member => member.IsStaff).ToList();

        var sheets = new List<SheetTable>
        {
            People("Юнаки", chosen, youth, tagNames),
            // The виховник's own гурток column is dropped: it says where they are placed, which
            // for a виховник is usually nowhere. What is wanted is where they are закріплені,
            // and that column is forced in whether or not the screen had it turned on.
            People("Впорядники", Staffwise(chosen), staff, tagNames),
            Tally(kurin.Branch, youth)
        };

        var content = _spreadsheet.Write(sheets);
        var stamp = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

        return new ServiceResult<RegistryFile>(
            ResultType.Success,
            new RegistryFile(content, $"reyestr-kurin-{KurinBranchNames.InFileName(kurin.Number)}-{stamp}.xlsx"));
    }

    /// <summary>One sheet of people, in the columns asked for, name first.</summary>
    private static SheetTable People(
        string name,
        IReadOnlyList<string> columnIds,
        IReadOnlyList<MemberDto> people,
        IReadOnlyDictionary<Guid, string> tagNames)
    {
        var headers = new List<string> { "Прізвище та ім'я" };
        headers.AddRange(columnIds.Select(id => RegistryColumnRules.HeaderFor(id)!));

        var rows = people
            .Select(member =>
            {
                var cells = new List<SheetCell> { SheetCell.Of(FullName(member)) };
                cells.AddRange(columnIds.Select(id => RegistryColumnRules.Read(id, member, tagNames)));
                return (IReadOnlyList<SheetCell>)cells;
            })
            .ToList();

        return new SheetTable(name, headers, rows);
    }

    /// <summary>The same columns, with placement swapped for закріплення and pulled to the front.</summary>
    private static List<string> Staffwise(IEnumerable<string> columnIds)
        => ["mentoredGroups", .. columnIds.Where(id => id is not ("groupName" or "mentoredGroups"))];

    /// <summary>
    /// How many юнаки stand at each ступінь. Counted by the ступінь they have reached — one
    /// person, one row — so the column sums to the юнацтво of the kurin and can be read against
    /// the roster above it. Впорядники are left out: they are counted as кадра, not as юнацтво.
    /// </summary>
    private static SheetTable Tally(KurinBranch branch, IReadOnlyList<MemberDto> youth)
    {
        var rows = new List<IReadOnlyList<SheetCell>>();

        foreach (var level in PlastLadderRules.DefaultFor(branch))
        {
            rows.Add([
                SheetCell.Of(RegistryColumnRules.HeaderFor($"level:{level}")),
                SheetCell.Count(youth.Count(member => member.LatestPlastLevel == level))
            ]);
        }

        // Everyone whose ступінь is outside this branch's ladder, or who has none recorded at
        // all. Without it the column would not add up to the roster, and a missing ступінь is
        // exactly the thing a провід is looking for in this table.
        var counted = PlastLadderRules.DefaultFor(branch).ToHashSet();
        var rest = youth.Count(member => member.LatestPlastLevel is null || !counted.Contains(member.LatestPlastLevel.Value));
        if (rest > 0)
        {
            rows.Add([SheetCell.Of("Без ступеня / інший"), SheetCell.Count(rest)]);
        }

        rows.Add([SheetCell.Of("Разом"), SheetCell.Count(youth.Count)]);

        return new SheetTable("Чисельність", ["Ступінь", "Кількість"], rows);
    }

    private static string FullName(MemberDto member)
        => PersonNames.SurnameFirst(member.LastName, member.FirstName, member.MiddleName);
}
