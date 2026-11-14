using MediatR;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe;

public sealed class GetProbeProgressQuery : IRequest<ServiceResult<ProbeProgressDto>>
{
    public GetProbeProgressQuery(Guid memberKey, string probeId)
    {
        MemberKey = memberKey;
        ProbeId = probeId;
    }

    public Guid MemberKey { get; }
    public string ProbeId { get; }
}

public sealed class GetProbeProgressQueryHandler : IRequestHandler<GetProbeProgressQuery, ServiceResult<ProbeProgressDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly IKurinJournalReader _journal;

    public GetProbeProgressQueryHandler(IUnitOfWork unitOfWork, IMemberDirectory members, IKurinJournalReader journal)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _journal = journal;
    }

    public async Task<ServiceResult<ProbeProgressDto>> Handle(GetProbeProgressQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProbeId))
        {
            return ServiceResult<ProbeProgressDto>.Failure(ResultType.BadRequest, ProbesAndBadgesErrors.ProbeIdRequired, "A probe id is required.");
        }

        var memberKurinKey = await _members.FindKurinKeyAsync(request.MemberKey, cancellationToken);
        if (memberKurinKey is null)
        {
            return ServiceResult<ProbeProgressDto>.Failure(ResultType.NotFound, ProbesAndBadgesErrors.MemberNotFound, "Member not found.");
        }

        var progress = await _unitOfWork.ProbeProgresses
            .GetByMemberAndProbeIdAsync(request.MemberKey, request.ProbeId, cancellationToken);

        var pointSignatures = await _unitOfWork.ProbePointProgresses
            .GetByMemberAndProbeAsync(request.MemberKey, request.ProbeId.Trim(), cancellationToken);

        var pointSignatureResponses = pointSignatures
            .OrderBy(x => x.PointId)
            .Select(x => new ProbePointProgressDto(
                x.ProbePointProgressKey,
                x.PointId,
                x.IsSigned,
                x.SignedAtUtc,
                x.SignedByUserKey,
                x.SignedByName,
                x.SignedByRole))
            .ToList();

        if (progress is null)
        {
            var notStarted = ProbeMapping.NotStarted(
                request.MemberKey,
                memberKurinKey.Value,
                request.ProbeId.Trim(),
                pointSignatureResponses);
            return new ServiceResult<ProbeProgressDto>(ResultType.Success, notStarted);
        }

        var journal = await _journal.GetForSubjectsAsync([progress.ProbeProgressKey], cancellationToken);
        return new ServiceResult<ProbeProgressDto>(
            ResultType.Success,
            ProbeMapping.ToDto(progress, journal, pointSignatureResponses));
    }
}
