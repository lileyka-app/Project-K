using MediatR;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Migration;

public sealed record GetMigrationPreflightReportQuery : IRequest<ServiceResult<MigrationPreflightReportDto>>;

public sealed class GetMigrationPreflightReportQueryHandler : IRequestHandler<GetMigrationPreflightReportQuery, ServiceResult<MigrationPreflightReportDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;

    public GetMigrationPreflightReportQueryHandler(
        IUnitOfWork unitOfWork,
        IMemberDirectory members)
    {
        _unitOfWork = unitOfWork;
        _members = members;
    }

    public async Task<ServiceResult<MigrationPreflightReportDto>> Handle(GetMigrationPreflightReportQuery request, CancellationToken cancellationToken)
    {
        var members = (await _members.GetAllAsync(cancellationToken)).ToList();
        var users = await _unitOfWork.Users.GetAllAsync(cancellationToken);

        // Identity keeps account addresses unique, so only members can share one.
        var duplicateEmails = members.GroupBy(m => m.Email.ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateEmailConflictDto(g.Key, g.Select(m => m.MemberKey).ToList(), []))
            .ToList();

        var orphanMembers = members
            .Where(m => m.UserKey.HasValue && !users.Any(u => u.Id == m.UserKey.Value))
            .Select(member => new OrphanMemberDto(member.MemberKey, member.FullName, member.Email, member.UserKey))
            .ToList();

        var linkedUserIds = members.Where(m => m.UserKey.HasValue).Select(m => m.UserKey!.Value).ToHashSet();
        var orphanUsers = users
            .Where(u => !linkedUserIds.Contains(u.Id))
            .Select(user => new OrphanUserDto(user.Id, user.UserName!, user.Email!))
            .ToList();

        // Inconsistent links used to compare the kurin snapshot on the account with where the
        // person stands. The snapshot is gone (DEBT-10): membership is the only place belonging is
        // written, so there is nothing left for it to contradict. The list stays in the report,
        // empty, so the admin page keeps its shape.
        var report = new MigrationPreflightReportDto(
            duplicateEmails,
            orphanMembers,
            orphanUsers,
            InconsistentLinks: [],
            TotalMembers: members.Count,
            TotalUsers: users.Count);

        return new ServiceResult<MigrationPreflightReportDto>(ResultType.Success, report);
    }
}
