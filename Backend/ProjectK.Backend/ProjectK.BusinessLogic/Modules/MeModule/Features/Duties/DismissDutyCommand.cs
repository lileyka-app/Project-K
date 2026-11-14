using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Duties;

/// <summary>
/// Takes one duty off the person's own «Справи» as it is now. Hiding it again records its size anew,
/// so it stays hidden until it outgrows that. Nobody else's list changes.
/// </summary>
public sealed record DismissDutyCommand(string Key, string Fingerprint) : IRequest<ServiceResult<Unit>>;

public sealed class DismissDutyCommandValidator : AbstractValidator<DismissDutyCommand>
{
    public DismissDutyCommandValidator()
    {
        RuleFor(c => c.Key).Must(DutyDismissals.IsKey).WithMessage("Not a duty key.");
        RuleFor(c => c.Fingerprint).NotEmpty().MaximumLength(DismissedDuty.FingerprintMaxLength);
    }
}

public sealed class DismissDutyCommandHandler : IRequestHandler<DismissDutyCommand, ServiceResult<Unit>>
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

    public async Task<ServiceResult<Unit>> Handle(DismissDutyCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userKey || userKey == Guid.Empty)
        {
            return MeErrors.SignInRequired<Unit>();
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

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
