using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest.Submit;

/// <summary>
/// A senior kurin asks the administrators to change its гілки to the ones named here. A kurin keeps
/// one request open: sending another replaces it.
/// </summary>
/// <param name="UspNumber">The wanted number in УСП; null leaves the kurin out of УСП.</param>
/// <param name="UpsNumber">The wanted number in УПС; null leaves the kurin out of УПС.</param>
public sealed record SubmitKurinBranchRequestCommand(Guid KurinKey, int? UspNumber, int? UpsNumber, string? Note)
    : IRequest<ServiceResult<KurinBranchRequestResponse>>;

public sealed class SubmitKurinBranchRequestCommandHandler
    : IRequestHandler<SubmitKurinBranchRequestCommand, ServiceResult<KurinBranchRequestResponse>>
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

    public async Task<ServiceResult<KurinBranchRequestResponse>> Handle(SubmitKurinBranchRequestCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userKey)
        {
            return ServiceResult<KurinBranchRequestResponse>.Failure(ResultType.Unauthorized, "Unauthorized", "Sign in to send a request.");
        }

        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return ServiceResult<KurinBranchRequestResponse>.Failure(ResultType.NotFound, "KurinNotFound", "Kurin not found.");
        }

        // A УПЮ kurin has one number it sets in its own settings; гілки change only for the older ones.
        if (!KurinBranchSet.IsSenior(kurin.Branch))
        {
            return ServiceResult<KurinBranchRequestResponse>.Failure(ResultType.BadRequest, "NotSeniorKurin", "Only a УСП or УПС kurin changes its гілки by request.");
        }

        var wanted = new KurinBranchRequest
        {
            KurinKey = kurin.KurinKey,
            RequestedByUserKey = userKey,
            UspNumber = request.UspNumber,
            UpsNumber = request.UpsNumber,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
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
                return ServiceResult<KurinBranchRequestResponse>.Failure(ResultType.Conflict, "KurinNumberTaken", "A kurin with this number already exists in this край.");
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
        var requesterName = requester is null ? null : $"{requester.FirstName} {requester.LastName}".Trim();
        var current = KurinBranchNames.DescribeSenior(kurin.Numbering);
        await _events.PublishAsync(
            new KurinBranchRequestSubmitted(
                wanted.KurinBranchRequestKey,
                kurin.KurinKey,
                kurin.NamedAfter is null ? current : $"«{kurin.NamedAfter}» · {current}",
                KurinBranchNames.DescribeSenior(wanted.WantedBranches),
                userKey),
            cancellationToken);

        return new ServiceResult<KurinBranchRequestResponse>(ResultType.Created, KurinBranchRequestResponse.From(wanted, kurin, requesterName));
    }

    private ServiceResult<KurinBranchRequestResponse>? Refusal(Common.Entities.KurinModule.Kurin kurin, KurinBranchRequest wanted)
    {
        if (wanted.Note?.Length > KurinBranchRequest.NoteMaxLength)
        {
            return ServiceResult<KurinBranchRequestResponse>.Failure(ResultType.BadRequest, "NoteTooLong", $"A note is up to {KurinBranchRequest.NoteMaxLength} characters.");
        }

        var branches = wanted.WantedBranches;
        if (branches.Count == 0)
        {
            return ServiceResult<KurinBranchRequestResponse>.Failure(ResultType.BadRequest, KurinBranchSet.InvalidCode, "A senior kurin keeps at least one гілка.");
        }

        if (branches.Any(b => !_numbers.IsAllowed(b.Number!.Value)))
        {
            return ServiceResult<KurinBranchRequestResponse>.Failure(
                ResultType.BadRequest,
                "KurinNumberOutOfRange",
                $"Kurin number must be between {KurinNumberOptions.MinNumber} and {_numbers.MaxNumber}.");
        }

        if (branches.SequenceEqual(kurin.Numbering))
        {
            return ServiceResult<KurinBranchRequestResponse>.Failure(ResultType.BadRequest, "NothingToChange", "The kurin already has these гілки and numbers.");
        }

        return null;
    }

}
