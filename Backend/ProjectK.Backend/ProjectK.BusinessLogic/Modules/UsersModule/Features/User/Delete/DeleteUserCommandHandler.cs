using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.User.Delete;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, ServiceResult<bool>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteUserCommandHandler(UserManager<AppUser> userManager, IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<bool>> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null)
        {
            return ServiceResult<bool>.Failure(ResultType.NotFound, "UserNotFound", "User not found.");
        }

        // A mentor's assignments reference the account, so they go first — read by the mentor,
        // not the whole table filtered in memory.
        var assignments = (await _unitOfWork.MentorAssignments
            .GetByMentorUserKeyAsync(request.UserId, cancellationToken)).ToList();

        if (assignments.Count > 0)
        {
            foreach (var assignment in assignments)
            {
                _unitOfWork.MentorAssignments.Delete(assignment, cancellationToken);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Hard delete the user
        var result = await _userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return ServiceResult<bool>.Failure(ResultType.InternalServerError, "UserNotDeleted", $"Failed to delete user: {errors}");
        }

        return new ServiceResult<bool>(ResultType.Success, true);
    }
}
