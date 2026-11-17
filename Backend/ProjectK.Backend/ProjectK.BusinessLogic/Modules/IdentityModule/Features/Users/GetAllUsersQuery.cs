using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Users;

public sealed class GetAllUsersQuery : IRequest<ServiceResult<IEnumerable<UserDto>>>
{
}

public sealed class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, ServiceResult<IEnumerable<UserDto>>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMembershipDirectory _memberships;

    public GetAllUsersQueryHandler(
        UserManager<AppUser> userManager,
        IUnitOfWork unitOfWork,
        IMembershipDirectory memberships)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _memberships = memberships;
    }
    public async Task<ServiceResult<IEnumerable<UserDto>>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _unitOfWork.Users.GetAllAsync(cancellationToken);

        // Where each account stands is read from membership in one go. The row shows a single
        // kurin because that is the shape of the list; someone standing in two is shown in the
        // one they joined most recently.
        var standing = await _memberships.GetCurrentForAccountsAsync(
            users.Select(user => user.Id).ToList(),
            cancellationToken);

        var kraiOf = (await _unitOfWork.Kurins.GetAllAsync(cancellationToken))
            .ToDictionary(kurin => kurin.KurinKey, kurin => kurin.KraiCode);

        List<UserDto> result = [];
        foreach (var user in users)
        {
            var isAdmin = await _userManager.IsInRoleAsync(user, SystemRoleNames.Admin);
            var here = standing.TryGetValue(user.Id, out var current) ? current.FirstOrDefault() : null;
            UserDto userDto = new()
            {
                UserId = user.Id,
                KurinKey = here?.KurinKey,
                KurinNumber = here?.KurinNumber,
                KurinBranch = here?.Branch,
                KurinBranches = here is null ? null : here.KurinBranches ?? [new KurinNumbering(here.Branch, here.KurinNumber)],
                KurinNamedAfter = here?.KurinNamedAfter,
                KurinKraiCode = here is not null && kraiOf.TryGetValue(here.KurinKey, out var krai) ? krai : null,
                Email = user.Email!,
                // Admin panel manages the system role only; offices are shown elsewhere.
                Role = isAdmin ? SystemRoleNames.Admin : SystemRoleNames.Member,
                TwoFactorEnabled = user.TwoFactorEnabled,
                IsSuspended = !user.CanSignIn(),
                FirstName = user.FirstName,
                LastName = user.LastName
            };
            result.Add(userDto);
        }
        return new ServiceResult<IEnumerable<UserDto>>(ResultType.Success, result);
    }
}
