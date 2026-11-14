using MediatR;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Photo;

/// <summary>
/// Puts a photo on a member or takes the current one off. <paramref name="Remove"/> and
/// <paramref name="Content"/> are mutually exclusive; neither means nothing happens.
/// </summary>
public sealed record SetMemberPhotoCommand(
    Guid MemberKey,
    Stream? Content,
    string? FileName,
    bool Remove) : IRequest<ServiceResult<string?>>;
