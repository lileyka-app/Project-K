using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Migration.PreflightReport;

public class GetMigrationPreflightReportQueryHandler : IRequestHandler<GetMigrationPreflightReportQuery, ServiceResult<MigrationPreflightReport>>
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

    public async Task<ServiceResult<MigrationPreflightReport>> Handle(GetMigrationPreflightReportQuery request, CancellationToken cancellationToken)
    {
        var report = new MigrationPreflightReport();

        var members = (await _members.GetAllAsync(cancellationToken)).ToList();
        var users = await _unitOfWork.Users.GetAllAsync(cancellationToken);

        report.TotalMembers = members.Count;
        report.TotalUsers = users.Count;

        // 1. Duplicate Email Conflicts
        var memberEmails = members.GroupBy(m => m.Email.ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateEmailConflict(g.Key, g.Select(m => m.MemberKey).ToList(), new List<Guid>()))
            .ToList();

        report.DuplicateEmailConflicts.AddRange(memberEmails);
        // Combine with cross-table duplicates if needed, but usually email is unique in Identity

        // 2. Orphan Members
        foreach (var member in members.Where(m => m.UserKey.HasValue && !users.Any(u => u.Id == m.UserKey.Value)))
        {
            report.OrphanMembers.Add(new OrphanMemberInfo(
                member.MemberKey,
                member.FullName,
                member.Email,
                member.UserKey));
        }

        // 3. Orphan Users (Users not linked to any member)
        var linkedUserIds = members.Where(m => m.UserKey.HasValue).Select(m => m.UserKey!.Value).ToHashSet();
        foreach (var user in users.Where(u => !linkedUserIds.Contains(u.Id)))
        {
            // Check if user is Admin - maybe admins don't need members? 
            // But for this project, every user should ideally have a member profile.
            report.OrphanUsers.Add(new OrphanUserInfo(user.Id, user.UserName!, user.Email!));
        }

        // 4. Inconsistent links used to compare the kurin snapshot on the account with where the
        // person stands. The snapshot is gone (DEBT-10): membership is the only place belonging is
        // written, so there is nothing left for it to contradict. The list stays in the report,
        // empty, so the admin page keeps its shape.

        return new ServiceResult<MigrationPreflightReport>(ResultType.Success, report);
    }
}
