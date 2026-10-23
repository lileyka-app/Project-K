using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuditModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe.Get;

public sealed class GetProbeProgressQuery : IRequest<ServiceResult<ProbeProgressResponse>>
{
    public GetProbeProgressQuery(Guid memberKey, string probeId)
    {
        MemberKey = memberKey;
        ProbeId = probeId;
    }

    public Guid MemberKey { get; }
    public string ProbeId { get; }
}

public sealed class GetProbeProgressQueryHandler : IRequestHandler<GetProbeProgressQuery, ServiceResult<ProbeProgressResponse>>
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

    public async Task<ServiceResult<ProbeProgressResponse>> Handle(GetProbeProgressQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProbeId))
        {
            return new ServiceResult<ProbeProgressResponse>(ResultType.BadRequest);
        }

        var memberKurinKey = await _members.FindKurinKeyAsync(request.MemberKey, cancellationToken);
        if (memberKurinKey is null)
        {
            return new ServiceResult<ProbeProgressResponse>(ResultType.NotFound);
        }

        var progress = await _unitOfWork.ProbeProgresses
            .GetByMemberAndProbeIdAsync(request.MemberKey, request.ProbeId, cancellationToken);

        var pointSignatures = await _unitOfWork.ProbePointProgresses
            .GetByMemberAndProbeAsync(request.MemberKey, request.ProbeId.Trim(), cancellationToken);

        var pointSignatureResponses = pointSignatures
            .OrderBy(x => x.PointId)
            .Select(x => new ProbePointProgressResponse(
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
            var notStarted = ProbeProgressResponse.CreateNotStarted(
                request.MemberKey,
                memberKurinKey.Value,
                request.ProbeId.Trim(),
                pointSignatureResponses);
            return new ServiceResult<ProbeProgressResponse>(ResultType.Success, notStarted);
        }

        var journal = await _journal.GetForSubjectsAsync([progress.ProbeProgressKey], cancellationToken);
        return new ServiceResult<ProbeProgressResponse>(
            ResultType.Success,
            ProbeProgressResponse.FromEntity(progress, journal, pointSignatureResponses));
    }
}
