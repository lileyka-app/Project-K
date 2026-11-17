using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Records.AuthModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Services;

/// <summary>
/// Who hears about a request between kurins. It goes to the Звʼязковий of a kurin and nobody else in
/// its провід: only that office answers or sends requests, and a kurin always has one.
/// </summary>
internal static class MembershipRequestNoticeRules
{
    public const string Route = "/kurin/registry/transfers";

    /// <summary>Where a person answers a kurin's invitation themself.</summary>
    public const string PersonRoute = "/invitations";

    public static async Task<IReadOnlyCollection<Guid>> ZvyazkoviAsync(IUnitOfWork unitOfWork, Guid kurinKey, CancellationToken cancellationToken)
    {
        var office = new MemberOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi);
        return (await unitOfWork.Leaderships.GetOfficeHoldersAsync(kurinKey, LeadershipType.KV, cancellationToken))
            .Where(holder => holder.UserKey.HasValue && holder.Office == office)
            .Select(holder => holder.UserKey!.Value)
            .Distinct()
            .ToList();
    }

    public static async Task<string> KurinAsync(IUnitOfWork unitOfWork, Guid kurinKey, CancellationToken cancellationToken)
    {
        var kurin = await unitOfWork.Kurins.GetByKeyAsync(kurinKey, cancellationToken);
        return kurin is null ? "інший курінь" : KurinBranchNames.InText(kurin);
    }
}
