using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

/// <summary>
/// The звіт куреня as a PDF. The heaviest read in the API — it walks membership, offices, probes
/// and badges to compose one document. <paramref name="TimeZone"/> is the reader's IANA zone.
/// </summary>
public sealed record ExportKurinReportPdfQuery(Guid KurinKey, string? TimeZone)
    : IRequest<ServiceResult<KurinReportPdf>>;

public sealed class ExportKurinReportPdfQueryHandler
    : IRequestHandler<ExportKurinReportPdfQuery, ServiceResult<KurinReportPdf>>
{
    private readonly KurinReportDataService _reportData;
    private readonly IKurinReportPdfRenderer _renderer;
    private readonly TimeProvider _timeProvider;

    public ExportKurinReportPdfQueryHandler(
        KurinReportDataService reportData,
        IKurinReportPdfRenderer renderer,
        TimeProvider timeProvider)
    {
        _reportData = reportData;
        _renderer = renderer;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<KurinReportPdf>> Handle(
        ExportKurinReportPdfQuery request,
        CancellationToken cancellationToken)
    {
        var report = await _reportData.BuildAsync(request.KurinKey, cancellationToken);
        if (report is null)
        {
            return ServiceResult<KurinReportPdf>.Failure(
                ResultType.NotFound, KurinErrors.KurinNotFound, "No report data exists for this kurin.");
        }

        var bytes = _renderer.Render(report, request.TimeZone);
        var fileName = $"kurin-{KurinBranchNames.InFileName(report.Kurin.Number)}-report-{_timeProvider.GetUtcNow():yyyyMMdd-HHmmss}.pdf";

        return new ServiceResult<KurinReportPdf>(ResultType.Success, new KurinReportPdf(bytes, fileName));
    }
}
