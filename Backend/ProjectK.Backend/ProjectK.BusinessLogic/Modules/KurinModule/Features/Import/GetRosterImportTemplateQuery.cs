using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Import;

/// <summary>
/// An empty roster for this kurin to fill in: one sheet, headings only, the columns its branch asks
/// for. Every heading is one the import recognises, so a filled-in template needs no mapping.
/// </summary>
public sealed record GetRosterImportTemplateQuery(Guid KurinKey) : IRequest<ServiceResult<RegistryFile>>;

public sealed class GetRosterImportTemplateQueryHandler
    : IRequestHandler<GetRosterImportTemplateQuery, ServiceResult<RegistryFile>>
{
    private readonly IUnitOfWork _kurinData;
    private readonly ISpreadsheetWriter _spreadsheet;

    public GetRosterImportTemplateQueryHandler(IUnitOfWork kurinData, ISpreadsheetWriter spreadsheet)
    {
        _kurinData = kurinData;
        _spreadsheet = spreadsheet;
    }

    public async Task<ServiceResult<RegistryFile>> Handle(
        GetRosterImportTemplateQuery request,
        CancellationToken cancellationToken)
    {
        var kurin = await _kurinData.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return KurinErrors.NotFound<RegistryFile>();
        }

        var headings = RosterImportRules.TemplateColumnsFor(kurin.Branch).Select(column => column.Heading).ToList();
        var content = _spreadsheet.Write([new SheetTable("Склад", headings, [])]);

        return new ServiceResult<RegistryFile>(
            ResultType.Success,
            new RegistryFile(content, $"import-kurin-{KurinBranchNames.InFileName(kurin.Number)}.xlsx"));
    }
}
