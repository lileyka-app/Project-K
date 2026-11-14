using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Models;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Common.Models.Dtos.ScoreModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Settings;

public sealed record GetKurinScoreSettingsQuery(Guid KurinKey) : IRequest<ServiceResult<KurinScoreSettingsResponse>>;

public sealed class GetKurinScoreSettingsQueryHandler : IRequestHandler<GetKurinScoreSettingsQuery, ServiceResult<KurinScoreSettingsResponse>>
{
    private readonly ScoreSettingsAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IUnitOfWork _unitOfWork;

    public GetKurinScoreSettingsQueryHandler(ScoreSettingsAccess access, ScoreBookReader books, IUnitOfWork unitOfWork)
    {
        _access = access;
        _books = books;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<KurinScoreSettingsResponse>> Handle(GetKurinScoreSettingsQuery request, CancellationToken cancellationToken)
    {
        if (await _access.RefuseAsync<KurinScoreSettingsResponse>(request.KurinKey, cancellationToken) is { } refused)
        {
            return refused;
        }

        var book = await _books.OpenAsync(request.KurinKey, cancellationToken);
        var categories = await _unitOfWork.AgendaCategories.GetForKurinAsync(request.KurinKey, includeArchived: false, cancellationToken);

        return new ServiceResult<KurinScoreSettingsResponse>(ResultType.Success, new KurinScoreSettingsResponse
        {
            KurinKey = request.KurinKey,
            Algorithm = book.Algorithm,
            AttendanceRates = categories
                .OrderBy(c => c.Name)
                .Select(c => new ScoreAttendanceRateDto
                {
                    AgendaCategoryKey = c.AgendaCategoryKey,
                    CategoryName = c.Name,
                    CategoryColorHex = c.ColorHex,
                    CategoryIcon = c.Icon,
                    Points = book.AttendanceRates.FirstOrDefault(r => r.AgendaCategoryKey == c.AgendaCategoryKey)?.Points ?? 0
                })
                .ToList(),
            Rules = book.Rules
                .Select(r => new ScoreRuleDto { Source = r.Source, Variant = r.Variant, FromDate = r.FromDate, Points = r.Points })
                .ToList(),
            Items = book.Items.Select(ScoreBook.ToDto).ToList(),
            Stages = book.Stages
                .OrderByDescending(s => s.FromDate)
                .Select(s => new ScoreStageDto { ScoreStageKey = s.ScoreStageKey, Name = s.Name, FromDate = s.FromDate, ToDate = s.ToDate })
                .ToList()
        });
    }
}
