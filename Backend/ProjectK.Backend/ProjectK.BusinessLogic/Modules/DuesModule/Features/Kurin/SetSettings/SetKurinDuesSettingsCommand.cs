using FluentValidation;
using MediatR;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin.SetSettings;

/// <summary>
/// Sets when the kurin's вкладка falls due: наперед or за пройдений квартал. Charges already made
/// stay; the next read of the box charges by the new choice, and a quarter is never charged twice.
/// </summary>
public sealed record SetKurinDuesSettingsCommand(Guid KurinKey, DuesChargeTiming ChargeTiming) : IRequest<ServiceResult<object>>;

public sealed class SetKurinDuesSettingsCommandValidator : AbstractValidator<SetKurinDuesSettingsCommand>
{
    public SetKurinDuesSettingsCommandValidator()
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.ChargeTiming).IsInEnum();
    }
}

public sealed class SetKurinDuesSettingsCommandHandler : IRequestHandler<SetKurinDuesSettingsCommand, ServiceResult<object>>
{
    private readonly IDuesUnitOfWork _dues;
    private readonly ICurrentUserContext _currentUser;

    public SetKurinDuesSettingsCommandHandler(IDuesUnitOfWork dues, ICurrentUserContext currentUser)
    {
        _dues = dues;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<object>> Handle(SetKurinDuesSettingsCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey != request.KurinKey)
        {
            return new ServiceResult<object>(ResultType.Forbidden);
        }

        var settings = await _dues.KurinDuesSettings.GetForKurinAsync(request.KurinKey, cancellationToken);
        if (settings is null)
        {
            settings = new KurinDuesSettings { KurinKey = request.KurinKey };
            _dues.KurinDuesSettings.Create(settings);
        }

        settings.ChargeTiming = request.ChargeTiming;
        settings.SetByUserKey = _currentUser.UserId;
        await _dues.SaveChangesAsync(cancellationToken);

        return new ServiceResult<object>(ResultType.Success);
    }
}
