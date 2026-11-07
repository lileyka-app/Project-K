using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Dtos.DuesModule.Requests;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Group.SetConcession;

/// <summary>Switches a person's пільга on or off from a quarter on. Earlier quarters keep what they had.</summary>
public sealed record SetDuesConcessionCommand(Guid BoxKey, Guid MembershipKey, SetDuesConcessionRequest Request)
    : IRequest<ServiceResult<object>>;

public sealed class SetDuesConcessionCommandValidator : AbstractValidator<SetDuesConcessionCommand>
{
    public SetDuesConcessionCommandValidator()
    {
        RuleFor(c => c.MembershipKey).NotEmpty();
        RuleFor(c => c.Request.FromQuarter.Number).InclusiveBetween(1, 4);
        RuleFor(c => c.Request.FromQuarter.Year).InclusiveBetween(2000, 2100);
    }
}

public sealed class SetDuesConcessionCommandHandler : IRequestHandler<SetDuesConcessionCommand, ServiceResult<object>>
{
    private readonly GroupDuesAccess _access;
    private readonly IDuesUnitOfWork _dues;
    private readonly IMembershipDirectory _memberships;
    private readonly ICurrentUserContext _currentUser;

    public SetDuesConcessionCommandHandler(
        GroupDuesAccess access,
        IDuesUnitOfWork dues,
        IMembershipDirectory memberships,
        ICurrentUserContext currentUser)
    {
        _access = access;
        _dues = dues;
        _memberships = memberships;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<object>> Handle(SetDuesConcessionCommand request, CancellationToken cancellationToken)
    {
        var (group, failure) = await _access.OpenBoxAsync<object>(request.BoxKey, cancellationToken);
        if (group is null)
        {
            return failure!;
        }

        var membership = (await _memberships.GetInKurinAsync(group.KurinKey, cancellationToken))
            .FirstOrDefault(m => m.MembershipKey == request.MembershipKey);
        if (membership is null || membership.GroupKey != group.GroupKey || membership.LeftAtUtc.HasValue)
        {
            return ServiceResult<object>.Failure(ResultType.NotFound, "NotInGroup", "They are not a youth of this гурток.");
        }

        var from = new DuesQuarter(request.Request.FromQuarter.Year, request.Request.FromQuarter.Number).Index;
        var existing = (await _dues.DuesConcessions.GetForKurinAsync(group.KurinKey, cancellationToken))
            .FirstOrDefault(c => c.MembershipKey == request.MembershipKey && c.FromQuarter == from);

        if (existing is null)
        {
            _dues.DuesConcessions.Create(new DuesConcession
            {
                KurinKey = group.KurinKey,
                MembershipKey = request.MembershipKey,
                FromQuarter = from,
                IsConcession = request.Request.IsConcession,
                SetByUserKey = _currentUser.UserId
            }, cancellationToken);
        }
        else
        {
            existing.IsConcession = request.Request.IsConcession;
            existing.SetByUserKey = _currentUser.UserId;
            existing.UpdatedDate = DateTime.UtcNow;
            _dues.DuesConcessions.Update(existing, cancellationToken);
        }

        await _dues.SaveChangesAsync(cancellationToken);
        return new ServiceResult<object>(ResultType.Success);
    }
}
