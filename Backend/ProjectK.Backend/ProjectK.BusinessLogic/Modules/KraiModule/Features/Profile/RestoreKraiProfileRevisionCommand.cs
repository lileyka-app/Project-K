using MediatR;
using ProjectK.BusinessLogic.Modules.KraiModule.Services;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KraiModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;

/// <summary>Brings an earlier revision back by saving its document again as the newest one.</summary>
public sealed record RestoreKraiProfileRevisionCommand(string KraiCode, int Revision)
    : IRequest<ServiceResult<KraiProfileEditorDto>>;

public sealed class RestoreKraiProfileRevisionCommandHandler : IRequestHandler<RestoreKraiProfileRevisionCommand, ServiceResult<KraiProfileEditorDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly KraiProfileEditor _editor;

    public RestoreKraiProfileRevisionCommandHandler(IUnitOfWork unitOfWork, KraiProfileEditor editor)
    {
        _unitOfWork = unitOfWork;
        _editor = editor;
    }

    public async Task<ServiceResult<KraiProfileEditorDto>> Handle(RestoreKraiProfileRevisionCommand request, CancellationToken cancellationToken)
    {
        var code = request.KraiCode.Trim().ToLowerInvariant();
        var source = await _unitOfWork.Krais.GetProfileRevisionAsync(code, request.Revision, cancellationToken);
        if (source is null)
        {
            return ServiceResult<KraiProfileEditorDto>.Failure(ResultType.NotFound, KraiErrors.RevisionNotFound, "Revision not found.");
        }

        var latest = await _unitOfWork.Krais.GetLatestProfileRevisionAsync(code, cancellationToken);
        return await _editor.SaveAsync(
            code,
            latest?.Revision ?? 0,
            KraiProfileEditor.TryParse(source.Json),
            $"Відновлено ревізію {source.Revision}",
            cancellationToken);
    }
}
