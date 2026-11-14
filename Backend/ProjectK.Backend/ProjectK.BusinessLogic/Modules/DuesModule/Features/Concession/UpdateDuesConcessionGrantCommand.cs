using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Dtos.DuesModule.Requests;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Concession;

/// <summary>Sets when a given пільга ends, or why it was given. Ending it is how it is taken away.</summary>
public sealed record UpdateDuesConcessionGrantCommand(Guid BoxKey, Guid GrantKey, UpdateDuesConcessionGrantRequest Request)
    : IRequest<ServiceResult<Unit>>;

public sealed class UpdateDuesConcessionGrantCommandValidator : AbstractValidator<UpdateDuesConcessionGrantCommand>
{
    public UpdateDuesConcessionGrantCommandValidator()
    {
        RuleFor(c => c.GrantKey).NotEmpty();
        RuleFor(c => c.Request.ToQuarter!.Number).InclusiveBetween(1, 4).When(c => c.Request.ToQuarter is not null);
        RuleFor(c => c.Request.ToQuarter!.Year).InclusiveBetween(2000, 2100).When(c => c.Request.ToQuarter is not null);
        RuleFor(c => c.Request.Reason).MaximumLength(500);
    }
}

public sealed class UpdateDuesConcessionGrantCommandHandler : IRequestHandler<UpdateDuesConcessionGrantCommand, ServiceResult<Unit>>
{
    private readonly DuesAccess _access;
    private readonly IDuesUnitOfWork _dues;
    private readonly IMembershipDirectory _memberships;
    private readonly ICurrentUserContext _currentUser;

    public UpdateDuesConcessionGrantCommandHandler(
        DuesAccess access,
        IDuesUnitOfWork dues,
        IMembershipDirectory memberships,
        ICurrentUserContext currentUser)
    {
        _access = access;
        _dues = dues;
        _memberships = memberships;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<Unit>> Handle(UpdateDuesConcessionGrantCommand request, CancellationToken cancellationToken)
    {
        var (grant, failure) = await DuesConcessionGrants.OpenAsync(_access, _dues, _memberships, request.BoxKey, request.GrantKey, cancellationToken);
        if (grant is null)
        {
            return failure!;
        }

        var to = DuesConcessionGrants.Index(request.Request.ToQuarter);
        if (to < grant.FromQuarter)
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, DuesErrors.EndsBeforeStart, "Пільга не може закінчитися раніше, ніж почнеться.");
        }

        // A later end may run into another grant of the same пільга, as a new grant would.
        if (await DuesConcessionGrants.OverlapsAsync(_dues, grant.KurinKey, grant.MembershipKey, grant.ConcessionKey, grant.FromQuarter, to, grant.DuesConcessionGrantKey, cancellationToken))
        {
            return ServiceResult<Unit>.Failure(ResultType.Conflict, DuesErrors.ConcessionOverlaps, "Ця пільга вже діє в цих кварталах.");
        }

        grant.ToQuarter = to;
        grant.Reason = DuesConcessionGrants.Reason(request.Request.Reason);
        grant.SetByUserKey = _currentUser.UserId;
        grant.UpdatedDate = DateTime.UtcNow;
        await _dues.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
