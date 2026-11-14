using FluentValidation;
using MediatR;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Dtos.DuesModule.Requests;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

internal static class DuesConcessionGrants
{
    public static int? Index(QuarterRequest? quarter) =>
        quarter is null ? null : new DuesQuarter(quarter.Year, quarter.Number).Index;

    /// <summary>Whether the person already holds this пільга in any of the quarters from..to, leaving out one grant.</summary>
    public static async Task<bool> OverlapsAsync(
        IDuesUnitOfWork dues, Guid kurinKey, Guid membershipKey, string concessionKey, int from, int? to, Guid? except, CancellationToken cancellationToken)
    {
        return (await dues.DuesConcessionGrants.GetForKurinAsync(kurinKey, cancellationToken))
            .Any(g => g.MembershipKey == membershipKey
                && g.ConcessionKey == concessionKey
                && g.DuesConcessionGrantKey != except
                && g.FromQuarter <= (to ?? int.MaxValue)
                && from <= (g.ToQuarter ?? int.MaxValue));
    }

    public static string? Reason(string? reason)
    {
        var trimmed = reason?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>The grant, when it belongs to someone who stands in the гурток the caller keeps.</summary>
    public static async Task<(DuesConcessionGrant? Grant, ServiceResult<Unit>? Failure)> OpenAsync(
        GroupDuesAccess access,
        IDuesUnitOfWork dues,
        IMembershipDirectory memberships,
        Guid boxKey,
        Guid grantKey,
        CancellationToken cancellationToken)
    {
        var (group, failure) = await access.OpenBoxAsync<Unit>(boxKey, cancellationToken);
        if (group is null)
        {
            return (null, failure);
        }

        var grant = await dues.DuesConcessionGrants.GetForUpdateAsync(grantKey, cancellationToken);
        var membership = grant is null || grant.KurinKey != group.KurinKey
            ? null
            : (await memberships.GetInKurinAsync(group.KurinKey, cancellationToken)).FirstOrDefault(m => m.MembershipKey == grant.MembershipKey);
        if (grant is null || membership is null || membership.GroupKey != group.GroupKey)
        {
            return (null, ServiceResult<Unit>.Failure(ResultType.NotFound, DuesErrors.GrantNotFound, "There is no such пільга in this гурток."));
        }

        return (grant, null);
    }
}
