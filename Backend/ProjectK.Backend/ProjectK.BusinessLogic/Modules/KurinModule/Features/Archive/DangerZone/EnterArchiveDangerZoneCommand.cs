using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Archive.DangerZone;

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
    private readonly ArchiveDangerZone _zone;
    private readonly IReauthentication _reauthentication;
    private readonly ICurrentUserContext _currentUser;
    private readonly IMemberDirectory _members;
    private readonly IKurinAudit _audit;
    private readonly IDomainEventPublisher _events;

    public EnterArchiveDangerZoneCommandHandler(
        IUnitOfWork unitOfWork,
        ArchiveReach reach,
        ArchiveDangerZone zone,
        IReauthentication reauthentication,
        ICurrentUserContext currentUser,
        IMemberDirectory members,
        IKurinAudit audit,
        IDomainEventPublisher events)
    {
        _unitOfWork = unitOfWork;
        _reach = reach;
        _zone = zone;
        _reauthentication = reauthentication;
        _currentUser = currentUser;
        _members = members;
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
                await RecordAsync(AuditActions.Archive.DangerZoneRefused, userKey, request.KurinKey, method, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return ServiceResult<ArchiveDangerZoneDto>.Failure(ResultType.BadRequest, ArchiveErrors.WrongCredentials, "Wrong password or code.");
        }

        var (ticket, entry) = _zone.Open(userKey, request.KurinKey, proof.SecurityStamp!);
        await RecordAsync(AuditActions.Archive.DangerZoneEntered, userKey, request.KurinKey, method, cancellationToken);
        // An administrator acting in the kurin has no member card; the leadership is told the account's name.
        var actor = (await _members.FindByAccountAsync(userKey, cancellationToken))?.FullName ?? proof.AccountName ?? string.Empty;
        await _events.PublishAsync(new ArchiveDangerZoneEntered(request.KurinKey, actor, userKey), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<ArchiveDangerZoneDto>(ResultType.Success, new ArchiveDangerZoneDto(ticket, entry.ExpiresUtc, entry.DeletionsLeft));
    }

    private Task RecordAsync(string action, Guid userKey, Guid kurinKey, string method, CancellationToken cancellationToken) =>
        _audit.RecordAsync(
            new AuditRecord(AuditArea.Archive, action, userKey, "Небезпечний режим", Changes: AuditChanges.Added(("method", method)), KurinKey: kurinKey),
            cancellationToken);
}
