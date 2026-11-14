using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Models.Dtos.DuesModule.Requests;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Group;

/// <summary>Sets the гурток's part of the вкладка from a quarter on; a rate already set from that quarter is replaced.</summary>
public sealed record SetGroupDuesRateCommand(Guid GroupKey, SetGroupDuesRateRequest Request) : IRequest<ServiceResult<Unit>>;

public sealed class SetGroupDuesRateCommandValidator : AbstractValidator<SetGroupDuesRateCommand>
{
    public SetGroupDuesRateCommandValidator()
    {
        RuleFor(c => c.Request.FromQuarter.Number).InclusiveBetween(1, 4);
        RuleFor(c => c.Request.FromQuarter.Year).InclusiveBetween(2000, 2100);
        RuleFor(c => c.Request.GroupShare).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
    }
}

public sealed class SetGroupDuesRateCommandHandler : IRequestHandler<SetGroupDuesRateCommand, ServiceResult<Unit>>
{
    private readonly GroupDuesAccess _access;
    private readonly IDuesUnitOfWork _dues;
    private readonly ICurrentUserContext _currentUser;

    public SetGroupDuesRateCommandHandler(GroupDuesAccess access, IDuesUnitOfWork dues, ICurrentUserContext currentUser)
    {
        _access = access;
        _dues = dues;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<Unit>> Handle(SetGroupDuesRateCommand request, CancellationToken cancellationToken)
    {
        var (group, failure) = await _access.OpenAsync<Unit>(request.GroupKey, cancellationToken);
        if (group is null)
        {
            return failure!;
        }

        var from = new DuesQuarter(request.Request.FromQuarter.Year, request.Request.FromQuarter.Number).Index;
        var existing = (await _dues.GroupDuesRates.GetForKurinAsync(group.KurinKey, cancellationToken))
            .FirstOrDefault(r => r.GroupKey == group.GroupKey && r.FromQuarter == from);

        if (existing is null)
        {
            _dues.GroupDuesRates.Create(new GroupDuesRate
            {
                KurinKey = group.KurinKey,
                GroupKey = group.GroupKey,
                FromQuarter = from,
                GroupShare = request.Request.GroupShare,
                SetByUserKey = _currentUser.UserId
            }, cancellationToken);
        }
        else
        {
            existing.GroupShare = request.Request.GroupShare;
            existing.SetByUserKey = _currentUser.UserId;
            existing.UpdatedDate = DateTime.UtcNow;
            _dues.GroupDuesRates.Update(existing, cancellationToken);
        }

        await _dues.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
