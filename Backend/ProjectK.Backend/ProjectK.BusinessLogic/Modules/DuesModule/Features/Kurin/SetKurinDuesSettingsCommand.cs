using FluentValidation;
using MediatR;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Enums.DuesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin;

/// <summary>
/// Sets when the kurin's вкладка falls due: наперед or за пройдений квартал. Charges already made
/// stay; the next read of the box charges by the new choice, and a quarter is never charged twice.
/// </summary>
public sealed record SetKurinDuesSettingsCommand(Guid KurinKey, DuesChargeTiming ChargeTiming) : IRequest<ServiceResult<Unit>>;

public sealed class SetKurinDuesSettingsCommandValidator : AbstractValidator<SetKurinDuesSettingsCommand>
{
    public SetKurinDuesSettingsCommandValidator()
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.ChargeTiming).IsInEnum();
    }
}

public sealed class SetKurinDuesSettingsCommandHandler : IRequestHandler<SetKurinDuesSettingsCommand, ServiceResult<Unit>>
{
    private readonly IDuesUnitOfWork _dues;
    private readonly ICurrentUserContext _currentUser;

    public SetKurinDuesSettingsCommandHandler(IDuesUnitOfWork dues, ICurrentUserContext currentUser)
    {
        _dues = dues;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<Unit>> Handle(SetKurinDuesSettingsCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey != request.KurinKey)
        {
            return DuesErrors.Refused<Unit>();
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

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
