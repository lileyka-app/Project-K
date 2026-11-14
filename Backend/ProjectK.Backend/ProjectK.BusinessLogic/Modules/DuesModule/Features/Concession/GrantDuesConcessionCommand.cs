using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Dtos.DuesModule.Requests;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.DuesModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Concession;

/// <summary>
/// Gives a youth of the гурток one of the край's пільги. The край's rule is copied onto the grant,
/// so editing the rule later leaves what was given alone.
/// </summary>
public sealed record GrantDuesConcessionCommand(Guid BoxKey, Guid MembershipKey, GrantDuesConcessionRequest Request)
    : IRequest<ServiceResult<Guid>>;

public sealed class GrantDuesConcessionCommandValidator : AbstractValidator<GrantDuesConcessionCommand>
{
    public GrantDuesConcessionCommandValidator()
    {
        RuleFor(c => c.MembershipKey).NotEmpty();
        RuleFor(c => c.Request.ConcessionKey).NotEmpty().MaximumLength(40);
        RuleFor(c => c.Request.FromQuarter.Number).InclusiveBetween(1, 4);
        RuleFor(c => c.Request.FromQuarter.Year).InclusiveBetween(2000, 2100);
        RuleFor(c => c.Request.ToQuarter!.Number).InclusiveBetween(1, 4).When(c => c.Request.ToQuarter is not null);
        RuleFor(c => c.Request.ToQuarter!.Year).InclusiveBetween(2000, 2100).When(c => c.Request.ToQuarter is not null);
        RuleFor(c => c.Request.Reason).MaximumLength(500);
    }
}

public sealed class GrantDuesConcessionCommandHandler : IRequestHandler<GrantDuesConcessionCommand, ServiceResult<Guid>>
{
    private readonly DuesAccess _access;
    private readonly IDuesUnitOfWork _dues;
    private readonly IMembershipDirectory _memberships;
    private readonly IKraiProfileDirectory _profiles;
    private readonly ICurrentUserContext _currentUser;

    public GrantDuesConcessionCommandHandler(
        DuesAccess access,
        IDuesUnitOfWork dues,
        IMembershipDirectory memberships,
        IKraiProfileDirectory profiles,
        ICurrentUserContext currentUser)
    {
        _access = access;
        _dues = dues;
        _memberships = memberships;
        _profiles = profiles;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<Guid>> Handle(GrantDuesConcessionCommand request, CancellationToken cancellationToken)
    {
        var (group, failure) = await _access.OpenBoxAsync<Guid>(request.BoxKey, cancellationToken);
        if (group is null)
        {
            return failure!;
        }

        var membership = (await _memberships.GetInKurinAsync(group.KurinKey, cancellationToken))
            .FirstOrDefault(m => m.MembershipKey == request.MembershipKey);
        if (membership is null || membership.GroupKey != group.GroupKey || membership.LeftAtUtc.HasValue)
        {
            return ServiceResult<Guid>.Failure(ResultType.NotFound, DuesErrors.NotInGroup, "They are not a youth of this гурток.");
        }

        var profile = await _profiles.GetForKurinAsync(group.KurinKey, cancellationToken);
        var rule = profile?.Dues.Concessions.FirstOrDefault(c => c.Key == request.Request.ConcessionKey.Trim());
        if (rule is null)
        {
            return ServiceResult<Guid>.Failure(ResultType.BadRequest, DuesErrors.UnknownConcession, "У краю куреня немає такої пільги.");
        }

        var from = new DuesQuarter(request.Request.FromQuarter.Year, request.Request.FromQuarter.Number).Index;
        var to = DuesConcessionGrantRules.Index(request.Request.ToQuarter);
        if (to < from)
        {
            return ServiceResult<Guid>.Failure(ResultType.BadRequest, DuesErrors.EndsBeforeStart, "Пільга не може закінчитися раніше, ніж почнеться.");
        }

        // The same пільга twice over the same quarters would only hide which one counts.
        if (await DuesConcessionGrantRules.OverlapsAsync(_dues, group.KurinKey, request.MembershipKey, rule.Key, from, to, null, cancellationToken))
        {
            return ServiceResult<Guid>.Failure(ResultType.Conflict, DuesErrors.ConcessionOverlaps, "Ця пільга вже діє в цих кварталах.");
        }

        var grant = new DuesConcessionGrant
        {
            KurinKey = group.KurinKey,
            MembershipKey = request.MembershipKey,
            ConcessionKey = rule.Key,
            Name = rule.Name,
            Kind = rule.Kind,
            Value = rule.Value,
            Parts = rule.Parts,
            FromQuarter = from,
            ToQuarter = to,
            Reason = TextRules.TrimOrNull(request.Request.Reason),
            SetByUserKey = _currentUser.UserId
        };
        _dues.DuesConcessionGrants.Create(grant, cancellationToken);
        await _dues.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Guid>(ResultType.Created, grant.DuesConcessionGrantKey);
    }
}
