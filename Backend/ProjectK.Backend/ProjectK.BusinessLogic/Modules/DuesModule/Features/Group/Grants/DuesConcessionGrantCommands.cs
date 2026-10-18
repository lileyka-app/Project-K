using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Dtos.DuesModule.Requests;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Group.Grants;

/// <summary>
/// Gives a youth of the гурток one of the край's пільги. The край's rule is copied onto the grant,
/// so editing the rule later leaves what was given alone.
/// </summary>
public sealed record GrantDuesConcessionCommand(Guid GroupKey, Guid MembershipKey, GrantDuesConcessionRequest Request)
    : IRequest<ServiceResult<Guid>>;

/// <summary>Sets when a given пільга ends, or why it was given. Ending it is how it is taken away.</summary>
public sealed record UpdateDuesConcessionGrantCommand(Guid GroupKey, Guid GrantKey, UpdateDuesConcessionGrantRequest Request)
    : IRequest<ServiceResult<object>>;

/// <summary>Removes a пільга given by mistake, with what it took off the quarters it covered.</summary>
public sealed record DeleteDuesConcessionGrantCommand(Guid GroupKey, Guid GrantKey) : IRequest<ServiceResult<object>>;

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

public sealed class GrantDuesConcessionCommandHandler : IRequestHandler<GrantDuesConcessionCommand, ServiceResult<Guid>>
{
    private readonly GroupDuesAccess _access;
    private readonly IDuesUnitOfWork _dues;
    private readonly IMembershipDirectory _memberships;
    private readonly IKraiProfileProvider _profiles;
    private readonly ICurrentUserContext _currentUser;

    public GrantDuesConcessionCommandHandler(
        GroupDuesAccess access,
        IDuesUnitOfWork dues,
        IMembershipDirectory memberships,
        IKraiProfileProvider profiles,
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
        var (group, failure) = await _access.OpenAsync<Guid>(request.GroupKey, cancellationToken);
        if (group is null)
        {
            return failure!;
        }

        var membership = (await _memberships.GetInKurinAsync(group.KurinKey, cancellationToken))
            .FirstOrDefault(m => m.MembershipKey == request.MembershipKey);
        if (membership is null || membership.GroupKey != group.GroupKey || membership.LeftAtUtc.HasValue)
        {
            return ServiceResult<Guid>.Failure(ResultType.NotFound, "NotInGroup", "They are not a youth of this гурток.");
        }

        var profile = await _profiles.GetForKurinAsync(group.KurinKey, cancellationToken);
        var rule = profile?.Dues.Concessions.FirstOrDefault(c => c.Key == request.Request.ConcessionKey.Trim());
        if (rule is null)
        {
            return ServiceResult<Guid>.Failure(ResultType.BadRequest, "UnknownConcession", "У краю куреня немає такої пільги.");
        }

        var from = new DuesQuarter(request.Request.FromQuarter.Year, request.Request.FromQuarter.Number).Index;
        var to = DuesConcessionGrants.Index(request.Request.ToQuarter);
        if (to < from)
        {
            return ServiceResult<Guid>.Failure(ResultType.BadRequest, "EndsBeforeStart", "Пільга не може закінчитися раніше, ніж почнеться.");
        }

        // The same пільга twice over the same quarters would only hide which one counts.
        var overlaps = (await _dues.DuesConcessionGrants.GetForKurinAsync(group.KurinKey, cancellationToken))
            .Any(g => g.MembershipKey == request.MembershipKey
                && g.ConcessionKey == rule.Key
                && g.FromQuarter <= (to ?? int.MaxValue)
                && from <= (g.ToQuarter ?? int.MaxValue));
        if (overlaps)
        {
            return ServiceResult<Guid>.Failure(ResultType.Conflict, "ConcessionOverlaps", "Ця пільга вже діє в цих кварталах.");
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
            Reason = DuesConcessionGrants.Reason(request.Request.Reason),
            SetByUserKey = _currentUser.UserId
        };
        _dues.DuesConcessionGrants.Create(grant, cancellationToken);
        await _dues.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Guid>(ResultType.Created, grant.DuesConcessionGrantKey);
    }
}

public sealed class UpdateDuesConcessionGrantCommandHandler : IRequestHandler<UpdateDuesConcessionGrantCommand, ServiceResult<object>>
{
    private readonly GroupDuesAccess _access;
    private readonly IDuesUnitOfWork _dues;
    private readonly IMembershipDirectory _memberships;
    private readonly ICurrentUserContext _currentUser;

    public UpdateDuesConcessionGrantCommandHandler(
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

    public async Task<ServiceResult<object>> Handle(UpdateDuesConcessionGrantCommand request, CancellationToken cancellationToken)
    {
        var (grant, failure) = await DuesConcessionGrants.OpenAsync(_access, _dues, _memberships, request.GroupKey, request.GrantKey, cancellationToken);
        if (grant is null)
        {
            return failure!;
        }

        var to = DuesConcessionGrants.Index(request.Request.ToQuarter);
        if (to < grant.FromQuarter)
        {
            return ServiceResult<object>.Failure(ResultType.BadRequest, "EndsBeforeStart", "Пільга не може закінчитися раніше, ніж почнеться.");
        }

        grant.ToQuarter = to;
        grant.Reason = DuesConcessionGrants.Reason(request.Request.Reason);
        grant.SetByUserKey = _currentUser.UserId;
        grant.UpdatedDate = DateTime.UtcNow;
        await _dues.SaveChangesAsync(cancellationToken);

        return new ServiceResult<object>(ResultType.Success);
    }
}

public sealed class DeleteDuesConcessionGrantCommandHandler : IRequestHandler<DeleteDuesConcessionGrantCommand, ServiceResult<object>>
{
    private readonly GroupDuesAccess _access;
    private readonly IDuesUnitOfWork _dues;
    private readonly IMembershipDirectory _memberships;

    public DeleteDuesConcessionGrantCommandHandler(GroupDuesAccess access, IDuesUnitOfWork dues, IMembershipDirectory memberships)
    {
        _access = access;
        _dues = dues;
        _memberships = memberships;
    }

    public async Task<ServiceResult<object>> Handle(DeleteDuesConcessionGrantCommand request, CancellationToken cancellationToken)
    {
        var (grant, failure) = await DuesConcessionGrants.OpenAsync(_access, _dues, _memberships, request.GroupKey, request.GrantKey, cancellationToken);
        if (grant is null)
        {
            return failure!;
        }

        _dues.DuesConcessionGrants.Delete(grant, cancellationToken);
        await _dues.SaveChangesAsync(cancellationToken);
        return new ServiceResult<object>(ResultType.Success);
    }
}

internal static class DuesConcessionGrants
{
    public static int? Index(QuarterRequest? quarter) =>
        quarter is null ? null : new DuesQuarter(quarter.Year, quarter.Number).Index;

    public static string? Reason(string? reason)
    {
        var trimmed = reason?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>The grant, when it belongs to someone who stands in the гурток the caller keeps.</summary>
    public static async Task<(DuesConcessionGrant? Grant, ServiceResult<object>? Failure)> OpenAsync(
        GroupDuesAccess access,
        IDuesUnitOfWork dues,
        IMembershipDirectory memberships,
        Guid groupKey,
        Guid grantKey,
        CancellationToken cancellationToken)
    {
        var (group, failure) = await access.OpenAsync<object>(groupKey, cancellationToken);
        if (group is null)
        {
            return (null, failure);
        }

        var grant = await dues.DuesConcessionGrants.GetForUpdateAsync(grantKey, cancellationToken);
        var membership = grant is null || grant.KurinKey != group.KurinKey
            ? null
            : (await memberships.GetInKurinAsync(group.KurinKey, cancellationToken)).FirstOrDefault(m => m.MembershipKey == grant.MembershipKey);
        if (grant is null || membership?.GroupKey != group.GroupKey)
        {
            return (null, ServiceResult<object>.Failure(ResultType.NotFound, "GrantNotFound", "There is no such пільга in this гурток."));
        }

        return (grant, null);
    }
}
