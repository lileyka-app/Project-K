using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Reports;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberWarning.Assign;

public sealed class AssignMemberWarningCommand : IRequest<ServiceResult<MemberWarningDto>>
{
    public AssignMemberWarningCommand(Guid memberKey, MemberWarningLevel level)
    {
        MemberKey = memberKey;
        Level = level;
    }

    public Guid MemberKey { get; }
    public MemberWarningLevel Level { get; }
}

public sealed class AssignMemberWarningCommandHandler : IRequestHandler<AssignMemberWarningCommand, ServiceResult<MemberWarningDto>>
{
    private static readonly IReadOnlyDictionary<MemberWarningLevel, int> LevelDurationsInMonths =
        new Dictionary<MemberWarningLevel, int>
        {
            [MemberWarningLevel.Level1] = 3,
            [MemberWarningLevel.Level2] = 6,
            [MemberWarningLevel.Level3] = 12
        };

    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IDomainEventPublisher _events;
    private readonly IMapper _mapper;
    private readonly IResourceAccessService _access;
    private readonly IKurinAudit _audit;

    public AssignMemberWarningCommandHandler(
        IMemberUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IDomainEventPublisher events,
        IMapper mapper,
        IResourceAccessService access,
        IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _events = events;
        _mapper = mapper;
        _access = access;
        _audit = audit;
    }

    public async Task<ServiceResult<MemberWarningDto>> Handle(AssignMemberWarningCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(MemberWarningLevel), request.Level))
        {
            return new ServiceResult<MemberWarningDto>(ResultType.BadRequest);
        }

        if (!_currentUserContext.UserId.HasValue)
        {
            return new ServiceResult<MemberWarningDto>(ResultType.Unauthorized);
        }

        var member = await _unitOfWork.Members.GetByKeyAsync(request.MemberKey, cancellationToken);
        if (member is null)
        {
            return new ServiceResult<MemberWarningDto>(ResultType.NotFound);
        }

        // The permission at the endpoint covers every level; which levels this person may give is
        // the kurin's policy.
        var level = await _access.CheckCapabilityAsync(
            KurinCapabilities.GiveWarning(request.Level), ResourceType.Member, request.MemberKey, cancellationToken);
        if (!level.IsAllowed)
        {
            return ServiceResult<MemberWarningDto>.Failure(
                ResultType.Forbidden,
                "WarningLevelNotAllowed",
                "The kurin's policy does not let you give a warning of this level.");
        }

        var now = DateTime.UtcNow;
        var activeWarnings = await _unitOfWork.MemberWarnings
            .GetActiveByMemberKeyAsync(request.MemberKey, now, cancellationToken);

        var activeLevels = activeWarnings.Select(w => w.Level).ToHashSet();

        if (!CanAssignLevel(request.Level, activeLevels))
        {
            return new ServiceResult<MemberWarningDto>(ResultType.Conflict);
        }

        var expiresAtUtc = now.AddMonths(LevelDurationsInMonths[request.Level]);

        var warningEntity = new Common.Entities.KurinModule.MemberWarning
        {
            MemberKey = request.MemberKey,
            KurinKey = _currentUserContext.KurinKey ?? Guid.Empty,
            Level = request.Level,
            IssuedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc,
            IssuedByUserKey = _currentUserContext.UserId.Value,
            UpdatedDate = now
        };

        _unitOfWork.MemberWarnings.Create(warningEntity, cancellationToken);

        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Warning,
                AuditActions.Warning.Given,
                warningEntity.MemberWarningKey,
                KurinReportTerminology.WarningLevel(warningEntity.Level),
                warningEntity.MemberKey,
                AuditChanges.Added(
                    ("level", warningEntity.Level),
                    ("expiresAtUtc", warningEntity.ExpiresAtUtc))),
            cancellationToken);

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            return new ServiceResult<MemberWarningDto>(ResultType.InternalServerError);
        }

        await NotifyMemberOwnerAsync(member, warningEntity, cancellationToken);

        var response = _mapper.Map<MemberWarningDto>(warningEntity);
        return new ServiceResult<MemberWarningDto>(ResultType.Created, response);
    }

    private static bool CanAssignLevel(MemberWarningLevel requestedLevel, IReadOnlySet<MemberWarningLevel> activeLevels)
    {
        return requestedLevel switch
        {
            MemberWarningLevel.Level1 => !activeLevels.Contains(MemberWarningLevel.Level1),
            MemberWarningLevel.Level2 => activeLevels.Contains(MemberWarningLevel.Level1) && !activeLevels.Contains(MemberWarningLevel.Level2),
            MemberWarningLevel.Level3 => activeLevels.Contains(MemberWarningLevel.Level2) && !activeLevels.Contains(MemberWarningLevel.Level3),
            _ => false
        };
    }

    private async Task NotifyMemberOwnerAsync(
        Common.Entities.KurinModule.Member member,
        Common.Entities.KurinModule.MemberWarning warning,
        CancellationToken cancellationToken)
    {
        if (!member.UserKey.HasValue)
        {
            return;
        }

        await _events.PublishAsync(
            new MemberWarningAssigned(
                warning.MemberWarningKey,
                member.MemberKey,
                member.UserKey.Value,
                warning.Level,
                _currentUserContext.UserId),
            cancellationToken);
    }

    private static string GetWarningLevelName(MemberWarningLevel level) =>
        level switch
        {
            MemberWarningLevel.Level1 => "першу пересторогу",
            MemberWarningLevel.Level2 => "другу пересторогу",
            MemberWarningLevel.Level3 => "третю пересторогу",
            _ => "пересторогу"
        };
}
