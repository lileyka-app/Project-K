using MediatR;
using ProjectK.BusinessLogic.Modules.ArchiveModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ArchiveModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Events.ArchiveModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Modules.ArchiveModule.Features.Archive;

/// <summary>
/// Turns on the archive's danger zone for the Звʼязковий after they prove themselves again, with
/// the password or with an authenticator or recovery code. Every attempt is journalled, the wrong
/// ones without what was typed, and the rest of the kurin's leadership hears of the right one.
/// </summary>
public sealed record EnterArchiveDangerZoneCommand(Guid KurinKey, string? Password, string? Code) : IRequest<ServiceResult<ArchiveDangerZoneDto>>;

public sealed class EnterArchiveDangerZoneCommandHandler : IRequestHandler<EnterArchiveDangerZoneCommand, ServiceResult<ArchiveDangerZoneDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ArchiveReach _reach;
    private readonly ArchiveDangerZoneService _zone;
    private readonly IReauthentication _reauthentication;
    private readonly ICurrentUserContext _currentUser;
    private readonly ICurrentMember _currentMember;
    private readonly IKurinAudit _audit;
    private readonly IDomainEventPublisher _events;

    public EnterArchiveDangerZoneCommandHandler(
        IUnitOfWork unitOfWork,
        ArchiveReach reach,
        ArchiveDangerZoneService zone,
        IReauthentication reauthentication,
        ICurrentUserContext currentUser,
        ICurrentMember currentMember,
        IKurinAudit audit,
        IDomainEventPublisher events)
    {
        _unitOfWork = unitOfWork;
        _reach = reach;
        _zone = zone;
        _reauthentication = reauthentication;
        _currentUser = currentUser;
        _currentMember = currentMember;
        _audit = audit;
        _events = events;
    }

    public async Task<ServiceResult<ArchiveDangerZoneDto>> Handle(EnterArchiveDangerZoneCommand request, CancellationToken cancellationToken)
    {
        if (await _reach.ResolveAsync(request.KurinKey, ResourceAction.Delete, cancellationToken) is not { KurinWide: true }
            || _currentUser.UserId is not { } userKey)
        {
            return ServiceResult<ArchiveDangerZoneDto>.Failure(ResultType.Forbidden, ArchiveErrors.OutOfReach, "Only the Звʼязковий deletes from the archive.");
        }

        var proof = await _reauthentication.VerifyAsync(userKey, request.Password, request.Code, cancellationToken);
        var method = proof.Method == ReauthenticationMethod.Password ? "password" : "code";
        switch (proof.Outcome)
        {
            case ReauthenticationOutcome.Paused:
                return ServiceResult<ArchiveDangerZoneDto>.Failure(ResultType.TooManyRequests, ArchiveErrors.TooManyAttempts, "Too many wrong attempts; try later.");
            case ReauthenticationOutcome.NoSecondFactor:
                return ServiceResult<ArchiveDangerZoneDto>.Failure(ResultType.BadRequest, ArchiveErrors.NoSecondFactor, "The account has no second factor; use the password.");
            case ReauthenticationOutcome.Wrong:
                // Not 401: the session is fine, and the client would take a 401 for a lost one.
                await _audit.RecordAsync(ArchiveDangerZoneService.JournalEntry(AuditActionNames.Archive.DangerZoneRefused, userKey, request.KurinKey, "method", method), cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return ServiceResult<ArchiveDangerZoneDto>.Failure(ResultType.BadRequest, ArchiveErrors.WrongCredentials, "Wrong password or code.");
        }

        var (ticket, entry) = _zone.Open(userKey, request.KurinKey, proof.SecurityStamp!);
        await _audit.RecordAsync(ArchiveDangerZoneService.JournalEntry(AuditActionNames.Archive.DangerZoneEntered, userKey, request.KurinKey, "method", method), cancellationToken);
        // An administrator acting in the kurin has no member card; the leadership is told the account's name.
        var actor = (await _currentMember.FindAsync(cancellationToken))?.FullName ?? proof.AccountName ?? string.Empty;
        await _events.PublishAsync(new ArchiveDangerZoneEntered(request.KurinKey, actor, userKey), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<ArchiveDangerZoneDto>(ResultType.Success, new ArchiveDangerZoneDto(ticket, entry.ExpiresUtc, entry.DeletionsLeft));
    }
}
