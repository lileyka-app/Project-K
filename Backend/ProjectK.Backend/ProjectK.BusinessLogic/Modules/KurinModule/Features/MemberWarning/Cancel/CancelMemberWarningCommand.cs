using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberWarning.Cancel;

public sealed class CancelMemberWarningCommand : IRequest<ServiceResult<MemberWarningDto>>
{
    public CancelMemberWarningCommand(Guid memberKey, Guid warningKey)
    {
        MemberKey = memberKey;
        WarningKey = warningKey;
    }

    public Guid MemberKey { get; }
    public Guid WarningKey { get; }
}

public sealed class CancelMemberWarningCommandHandler : IRequestHandler<CancelMemberWarningCommand, ServiceResult<MemberWarningDto>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IMapper _mapper;
    private readonly TimeProvider _timeProvider;
    private readonly IResourceAccessService _access;

    public CancelMemberWarningCommandHandler(
        IMemberUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IMapper mapper,
        TimeProvider timeProvider,
        IResourceAccessService access)
    {
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _mapper = mapper;
        _timeProvider = timeProvider;
        _access = access;
    }

    public async Task<ServiceResult<MemberWarningDto>> Handle(CancelMemberWarningCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return new ServiceResult<MemberWarningDto>(ResultType.Unauthorized);
        }

        var warning = await _unitOfWork.MemberWarnings.GetByKeyAsync(request.WarningKey, cancellationToken);
        if (warning is null || warning.MemberKey != request.MemberKey)
        {
            return new ServiceResult<MemberWarningDto>(ResultType.NotFound);
        }

        if (warning.RevokedAtUtc.HasValue || warning.ExpiresAtUtc <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            return new ServiceResult<MemberWarningDto>(ResultType.Conflict);
        }

        if (!await MayCancelAsync(warning, _currentUserContext.UserId.Value, cancellationToken))
        {
            return new ServiceResult<MemberWarningDto>(ResultType.Forbidden);
        }

        warning.RevokedAtUtc = DateTime.UtcNow;
        warning.RevokedByUserKey = _currentUserContext.UserId.Value;
        warning.UpdatedDate = warning.RevokedAtUtc.Value;

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (changes <= 0)
        {
            return new ServiceResult<MemberWarningDto>(ResultType.InternalServerError);
        }

        var response = _mapper.Map<MemberWarningDto>(warning);
        return new ServiceResult<MemberWarningDto>(ResultType.Success, response);
    }

    /// <summary>
    /// Whoever gave a пересторога may take it back while they could still give it; anyone else
    /// needs <see cref="KurinCapability.CancelWarning"/>.
    /// </summary>
    private async Task<bool> MayCancelAsync(
        Common.Entities.KurinModule.MemberWarning warning,
        Guid userKey,
        CancellationToken cancellationToken)
    {
        if (warning.IssuedByUserKey == userKey
            && (await _access.CheckCapabilityAsync(
                KurinCapabilities.GiveWarning(warning.Level), ResourceType.Member, warning.MemberKey, cancellationToken)).IsAllowed)
        {
            return true;
        }

        return (await _access.CheckCapabilityAsync(
            KurinCapability.CancelWarning, ResourceType.Member, warning.MemberKey, cancellationToken)).IsAllowed;
    }
}

