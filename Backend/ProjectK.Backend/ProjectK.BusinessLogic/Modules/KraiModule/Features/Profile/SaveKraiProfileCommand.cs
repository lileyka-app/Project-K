using MediatR;
using ProjectK.BusinessLogic.Modules.KraiModule.Services;
using ProjectK.Common.Dtos.KraiModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;

/// <summary>Saves the край's profile as a new revision. Also how an imported file is saved.</summary>
public sealed record SaveKraiProfileCommand(string KraiCode, int BaseRevision, KraiProfileDocument Document, string? Note)
    : IRequest<ServiceResult<KraiProfileEditorDto>>;

public sealed class SaveKraiProfileCommandHandler : IRequestHandler<SaveKraiProfileCommand, ServiceResult<KraiProfileEditorDto>>
{
    private readonly KraiProfileEditor _editor;

    public SaveKraiProfileCommandHandler(KraiProfileEditor editor)
    {
        _editor = editor;
    }

    public Task<ServiceResult<KraiProfileEditorDto>> Handle(SaveKraiProfileCommand request, CancellationToken cancellationToken)
    {
        return _editor.SaveAsync(request.KraiCode, request.BaseRevision, request.Document, request.Note, cancellationToken);
    }
}
