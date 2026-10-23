using FluentValidation;
using MediatR;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Exceptions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Duties;

/// <summary>
/// Takes one duty off the person's own «Справи» as it is now. Hiding it again records its size anew,
/// so it stays hidden until it outgrows that. Nobody else's list changes.
/// </summary>
public sealed record DismissDutyCommand(string Key, string Fingerprint) : IRequest<ServiceResult<object>>;

public sealed class DismissDutyCommandValidator : AbstractValidator<DismissDutyCommand>
{
    public DismissDutyCommandValidator()
    {
        RuleFor(c => c.Key).Must(DutyDismissals.IsKey).WithMessage("Not a duty key.");
        RuleFor(c => c.Fingerprint).NotEmpty().MaximumLength(DismissedDuty.FingerprintMaxLength);
    }
}

public sealed class DismissDutyCommandHandler : IRequestHandler<DismissDutyCommand, ServiceResult<object>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public DismissDutyCommandHandler(IUnitOfWork unitOfWork, ICurrentUserContext currentUser, TimeProvider time)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<object>> Handle(DismissDutyCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userKey || userKey == Guid.Empty)
        {
            return ServiceResult<object>.Failure(ResultType.Unauthorized, "NotSignedIn", "Not signed in.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var standing = (await _unitOfWork.DismissedDuties.GetToChangeAsync(userKey, request.Key, cancellationToken)).FirstOrDefault();
        if (standing is null)
        {
            _unitOfWork.DismissedDuties.Create(new DismissedDuty
            {
                UserKey = userKey,
                DutyKey = request.Key,
                Fingerprint = request.Fingerprint,
                DismissedAtUtc = now
            }, cancellationToken);
        }
        else
        {
            standing.Fingerprint = request.Fingerprint;
            standing.DismissedAtUtc = now;
            _unitOfWork.DismissedDuties.Update(standing, cancellationToken);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateRowException)
        {
            // The same duty hidden twice at once, from two tabs: one row stands, which is all that was asked.
        }

        return new ServiceResult<object>(ResultType.Success);
    }
}
