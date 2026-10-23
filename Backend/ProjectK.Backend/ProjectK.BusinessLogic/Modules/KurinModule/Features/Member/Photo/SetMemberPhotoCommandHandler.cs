using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Photo;

public class SetMemberPhotoCommandHandler
    : IRequestHandler<SetMemberPhotoCommand, ServiceResult<string?>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly IPhotoService _photoService;
    private readonly IKurinAudit _audit;

    public SetMemberPhotoCommandHandler(IMemberUnitOfWork unitOfWork, IPhotoService photoService, IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _photoService = photoService;
        _audit = audit;
    }

    public async Task<ServiceResult<string?>> Handle(
        SetMemberPhotoCommand request,
        CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Members.GetByKeyAsync(request.MemberKey, cancellationToken);
        if (member is null)
        {
            return new ServiceResult<string?>(ResultType.NotFound);
        }

        var previousBlobName = member.ProfilePhotoBlobName;

        if (request.Content is not null && !string.IsNullOrWhiteSpace(request.FileName))
        {
            PhotoUploadResult upload;
            try
            {
                upload = await _photoService.UploadPhotoAsync(request.Content, request.FileName, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // Storage refuses anything it cannot decode as an image; that is the caller's
                // mistake, not the server's.
                return ServiceResult<string?>.Failure(
                    ResultType.BadRequest,
                    "InvalidImage",
                    "The uploaded file is not a valid image.");
            }

            member.ProfilePhotoBlobName = upload.BlobName;
        }
        else if (request.Remove && previousBlobName is not null)
        {
            member.ProfilePhotoBlobName = null;
        }
        else
        {
            return new ServiceResult<string?>(ResultType.Success, previousBlobName);
        }

        if (string.Equals(previousBlobName, member.ProfilePhotoBlobName, StringComparison.Ordinal))
        {
            return new ServiceResult<string?>(ResultType.Success, previousBlobName);
        }

        // A verified profile is verified against the face on it, so a new photo puts the
        // verification back in the queue.
        var verificationBefore = member.ProfileVerificationStatus;
        if (member.ProfileVerificationStatus == MemberProfileVerificationStatus.VerifiedCurrent)
        {
            member.ProfileVerificationStatus = MemberProfileVerificationStatus.VerifiedStale;
        }

        _unitOfWork.Members.Update(member, cancellationToken);

        // Where the photo is stored is not the journal's business: it says only that it changed.
        await _audit.RecordAsync(
            new AuditRecord(
                AuditArea.Member,
                member.ProfilePhotoBlobName is null ? AuditActions.Member.PhotoRemoved : AuditActions.Member.PhotoChanged,
                member.MemberKey,
                MemberAuditFields.LabelOf(member),
                member.MemberKey,
                AuditChanges.Of((MemberAuditFields.ProfileVerification, verificationBefore, member.ProfileVerificationStatus))),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Only after the row no longer points at it — a blob deleted before the write would
        // leave a member pointing at nothing if the write then failed.
        if (previousBlobName is not null)
        {
            await _photoService.DeletePhotoAsync(previousBlobName, cancellationToken);
        }

        return new ServiceResult<string?>(ResultType.Success, member.ProfilePhotoBlobName);
    }
}
