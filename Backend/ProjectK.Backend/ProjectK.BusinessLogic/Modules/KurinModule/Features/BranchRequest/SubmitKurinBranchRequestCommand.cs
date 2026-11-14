using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Options.KurinModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.KurinModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest;

/// <summary>
/// A senior kurin asks the administrators to change its гілки to the ones named here. A kurin keeps
/// one request open: sending another replaces it.
/// </summary>
/// <param name="UspNumber">The wanted number in УСП; null leaves the kurin out of УСП.</param>
/// <param name="UpsNumber">The wanted number in УПС; null leaves the kurin out of УПС.</param>
public sealed record SubmitKurinBranchRequestCommand(Guid KurinKey, int? UspNumber, int? UpsNumber, string? Note)
    : IRequest<ServiceResult<KurinBranchRequestDto>>;

public sealed class SubmitKurinBranchRequestCommandHandler
    : IRequestHandler<SubmitKurinBranchRequestCommand, ServiceResult<KurinBranchRequestDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly UserManager<AppUser> _userManager;
    private readonly IDomainEventPublisher _events;
    private readonly KurinNumberOptions _numbers;

    public SubmitKurinBranchRequestCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        UserManager<AppUser> userManager,
        IDomainEventPublisher events,
        IOptions<KurinNumberOptions> numbers)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _userManager = userManager;
        _events = events;
        _numbers = numbers.Value;
    }

    public async Task<ServiceResult<KurinBranchRequestDto>> Handle(SubmitKurinBranchRequestCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userKey)
        {
            return ServiceResult<KurinBranchRequestDto>.Failure(ResultType.Unauthorized, MemberErrors.Unauthorized, "Sign in to send a request.");
        }

        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return ServiceResult<KurinBranchRequestDto>.Failure(ResultType.NotFound, MemberErrors.KurinNotFound, "Kurin not found.");
        }

        // A УПЮ kurin has one number it sets in its own settings; гілки change only for the older ones.
        if (!KurinBranchSet.IsSenior(kurin.Branch))
        {
            return ServiceResult<KurinBranchRequestDto>.Failure(ResultType.BadRequest, MemberErrors.NotSeniorKurin, "Only a УСП or УПС kurin changes its гілки by request.");
        }

        var wanted = new KurinBranchRequest
        {
            KurinKey = kurin.KurinKey,
            RequestedByUserKey = userKey,
            UspNumber = request.UspNumber,
            UpsNumber = request.UpsNumber,
            Note = TextRules.TrimOrNull(request.Note),
            RequestedAtUtc = DateTime.UtcNow
        };

        if (Refusal(kurin, wanted) is { } refused)
        {
            return refused;
        }

        foreach (var numbering in wanted.WantedBranches)
        {
            if (await _unitOfWork.Kurins.IsNumberTakenAsync(kurin.KraiCode, numbering.Branch, numbering.Number!.Value, kurin.KurinKey, cancellationToken))
            {
                return ServiceResult<KurinBranchRequestDto>.Failure(ResultType.Conflict, MemberErrors.KurinNumberTaken, "A kurin with this number already exists in this край.");
            }
        }

        var open = await _unitOfWork.KurinBranchRequests.GetPendingForKurinAsync(kurin.KurinKey, cancellationToken);
        if (open is not null)
        {
            _unitOfWork.KurinBranchRequests.Delete(open, cancellationToken);
        }

        _unitOfWork.KurinBranchRequests.Create(wanted, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var requester = await _userManager.FindByIdAsync(userKey.ToString());
        var requesterName = requester is null ? null : PersonNames.FirstLast(requester.FirstName, requester.LastName);
        var current = KurinBranchNames.DescribeSenior(kurin.Numbering);
        await _events.PublishAsync(
            new KurinBranchRequestSubmitted(
                wanted.KurinBranchRequestKey,
                kurin.KurinKey,
                kurin.NamedAfter is null ? current : $"«{kurin.NamedAfter}» · {current}",
                KurinBranchNames.DescribeSenior(wanted.WantedBranches),
                userKey),
            cancellationToken);

        return new ServiceResult<KurinBranchRequestDto>(ResultType.Created, wanted.ToDto(kurin, requesterName));
    }

    private ServiceResult<KurinBranchRequestDto>? Refusal(Common.Entities.KurinModule.Kurin kurin, KurinBranchRequest wanted)
    {
        if (wanted.Note?.Length > KurinBranchRequest.NoteMaxLength)
        {
            return ServiceResult<KurinBranchRequestDto>.Failure(ResultType.BadRequest, MemberErrors.NoteTooLong, $"A note is up to {KurinBranchRequest.NoteMaxLength} characters.");
        }

        var branches = wanted.WantedBranches;
        if (branches.Count == 0)
        {
            return ServiceResult<KurinBranchRequestDto>.Failure(ResultType.BadRequest, KurinBranchSet.InvalidCode, "A senior kurin keeps at least one гілка.");
        }

        if (branches.Any(b => !_numbers.IsAllowed(b.Number!.Value)))
        {
            return ServiceResult<KurinBranchRequestDto>.Failure(
                ResultType.BadRequest,
                MemberErrors.KurinNumberOutOfRange,
                _numbers.OutOfRangeMessage);
        }

        if (branches.SequenceEqual(kurin.Numbering))
        {
            return ServiceResult<KurinBranchRequestDto>.Failure(ResultType.BadRequest, MemberErrors.NothingToChange, "The kurin already has these гілки and numbers.");
        }

        return null;
    }

}
