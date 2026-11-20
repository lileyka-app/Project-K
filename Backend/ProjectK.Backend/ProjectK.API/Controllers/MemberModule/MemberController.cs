using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Filters;
using ProjectK.API.Models.Requests;
using ProjectK.API.Uploads;
using ProjectK.BusinessLogic.Modules.MemberModule;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Dtos.MemberModule.Requests;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.MemberModule;

namespace ProjectK.API.Controllers.MemberModule;

/// <summary>
/// Members: their records, the groups they belong to, and whether their profile has been checked by
/// leadership.
/// </summary>
[Route("api/member")]
[ApiController]
public class MemberController : ControllerBase
{
    private readonly IMediator _mediator;

    public MemberController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns one member.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{memberKey:guid}")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Read, "route:memberKey")]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByKey(Guid memberKey)
    {
        var request = new GetMemberByKeyQuery(memberKey);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// What the caller may do to this member under the kurin's policy — which перестороги to give
    /// and cancel, which відзначення to confirm, whether to sign probes — so the buttons shown are
    /// the ones the server accepts.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{memberKey:guid}/abilities")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Read, "route:memberKey")]
    [ProducesResponseType(typeof(MemberAbilitiesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAbilities(Guid memberKey)
    {
        var response = await _mediator.Send(new GetMemberAbilitiesQuery(memberKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns one person's archive box: who they are, what folders their history has, and the
    /// contents of the folders named in <paramref name="include"/> — a comma-separated list of
    /// <c>memberships</c>, <c>offices</c>, <c>levels</c>, <c>awards</c>, <c>warnings</c> and
    /// <c>progress</c>. Without it, only the index comes back.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{memberKey:guid}/dossier")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Read, "route:memberKey")]
    [ProducesResponseType(typeof(MemberDossierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDossier(Guid memberKey, [FromQuery] string? include)
    {
        var response = await _mediator.Send(new GetMemberDossierQuery(memberKey, ParseInclude(include)));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns one folder of a person's dossier and nothing else.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{memberKey:guid}/dossier/{folder}")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Read, "route:memberKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDossierFolder(Guid memberKey, string folder)
    {
        var response = await _mediator.Send(new GetMemberDossierQuery(memberKey, [folder]));
        if (response.Type != ResultType.Success)
        {
            return response.ToActionResult(this);
        }

        var dossier = response.Data!;
        return folder.ToLowerInvariant() switch
        {
            MemberDossierFolderNames.Memberships => Ok(dossier.Memberships),
            MemberDossierFolderNames.Offices => Ok(dossier.Offices),
            MemberDossierFolderNames.Levels => Ok(dossier.Levels),
            MemberDossierFolderNames.Awards => Ok(dossier.Awards),
            MemberDossierFolderNames.Warnings => Ok(dossier.Warnings),
            MemberDossierFolderNames.Progress => Ok(dossier.Progress),
            _ => Ok(dossier.Folders)
        };
    }

    // "all" opens every folder; anything else is taken apart and checked by the use case, so an
    // unknown name is answered the same way whichever route it arrived on.
    private static IReadOnlyCollection<string> ParseInclude(string? include)
    {
        if (string.IsNullOrWhiteSpace(include))
        {
            return [];
        }

        return string.Equals(include.Trim(), "all", StringComparison.OrdinalIgnoreCase)
            ? MemberDossierFolderNames.All
            : [.. include.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    /// <summary>
    /// Creates a member in a group, with an optional photo.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost]
    [ResourceAuthorize(ResourceType.Group, ResourceAction.Create, "arg:request.GroupKey")]
    [RequestSizeLimit(ImageUploadRules.MaxRequestBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create([FromForm] UpsertMemberRequest request,
                                            CancellationToken cancellationToken)
    {
        if (!request.GroupKey.HasValue || request.GroupKey.Value == Guid.Empty)
        {
            return this.Failure(ResultType.BadRequest, MemberErrors.GroupKeyRequired, "groupKey is required.");
        }

        if (this.RefuseImage(request.Blob) is { } refusal)
        {
            return refusal;
        }

        var command = new UpsertMemberCommand
        {
            GroupKey = request.GroupKey.Value,
            KurinKey = request.KurinKey,
            FirstName = request.FirstName,
            LastName = request.LastName,
            MiddleName = request.MiddleName,
            Email = request.Email ?? string.Empty,
            PhoneNumber = request.PhoneNumber,
            DateOfBirth = request.DateOfBirth,
            Address = request.Address,
            School = request.School,
            PlastLevelHistories = request.PlastLevelHistories,
            CardExtras = request.CardExtras,
            ApplyCardFields = true,
            CreateUserAccount = request.CreateUserAccount,
            BlobContent = request.Blob is { Length: > 0 } ? request.Blob.OpenReadStream() : null,
            BlobFileName = request.Blob?.FileName,
            BlobContentType = request.Blob?.ContentType
        }.WithParentsOf(request);
        var response = await _mediator.Send(command, cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Creates a member against a kurin rather than a specific group.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost("~/api/kurin/{kurinKey:guid}/members")]
    // Old address, kept for PWAs still on the previous build; drop in 1.5.
    [HttpPost("kurins/{kurinKey:guid}/members")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Create, "route:kurinKey")]
    [RequestSizeLimit(ImageUploadRules.MaxRequestBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateByKurin(Guid kurinKey,
        [FromForm] UpsertMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (this.RefuseImage(request.Blob) is { } refusal)
        {
            return refusal;
        }

        var command = new UpsertMemberCommand
        {
            KurinKey = kurinKey,
            GroupKey = null,
            FirstName = request.FirstName,
            LastName = request.LastName,
            MiddleName = request.MiddleName,
            Email = request.Email ?? string.Empty,
            PhoneNumber = request.PhoneNumber,
            DateOfBirth = request.DateOfBirth,
            Address = request.Address,
            School = request.School,
            PlastLevelHistories = request.PlastLevelHistories,
            CardExtras = request.CardExtras,
            ApplyCardFields = true,
            CreateUserAccount = request.CreateUserAccount,
            BlobContent = request.Blob is { Length: > 0 } ? request.Blob.OpenReadStream() : null,
            BlobFileName = request.Blob?.FileName,
            BlobContentType = request.Blob?.ContentType
        }.WithParentsOf(request);

        var response = await _mediator.Send(command, cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Rewrites a member's record.
    /// </summary>
    /// <remarks>
    /// Members may edit their own profile; editing somebody else's needs rights over that member.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("{memberKey:guid}")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Update, "route:memberKey")]
    [RequestSizeLimit(ImageUploadRules.MaxRequestBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(Guid memberKey,
                                            [FromForm] UpsertMemberRequest request,
                                            CancellationToken cancellationToken)
    {
        if (this.RefuseImage(request.Blob) is { } refusal)
        {
            return refusal;
        }

        var command = new UpsertMemberCommand
        {
            MemberKey = memberKey,
            GroupKey = request.GroupKey,
            KurinKey = request.KurinKey,
            FirstName = request.FirstName,
            LastName = request.LastName,
            MiddleName = request.MiddleName,
            Email = request.Email ?? string.Empty,
            PhoneNumber = request.PhoneNumber,
            DateOfBirth = request.DateOfBirth,
            Address = request.Address,
            School = request.School,
            PlastLevelHistories = request.PlastLevelHistories,
            CardExtras = request.CardExtras,
            ApplyCardFields = true,
            RemoveProfilePhoto = request.RemoveProfilePhoto ?? false,
            CreateUserAccount = request.CreateUserAccount,
            BlobContent = request.Blob is { Length: > 0 } ? request.Blob.OpenReadStream() : null,
            BlobFileName = request.Blob?.FileName,
            BlobContentType = request.Blob?.ContentType
        }.WithParentsOf(request);
        var response = await _mediator.Send(command, cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Sends a member whose account is not activated yet a fresh invitation.
    /// </summary>
    /// <remarks>
    /// Every invitation sent before stops working. The letter goes to the address on the member's
    /// record, which the account always shares while it waits to be claimed.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireGroupLeadership)]
    [HttpPost("{memberKey:guid}/invitation")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Update, "route:memberKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResendInvitation(Guid memberKey, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ResendMemberInvitationCommand(memberKey), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Marks a member's profile checked by leadership.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireGroupLeadership)]
    [HttpPut("{memberKey:guid}/profile-verification")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Update, "route:memberKey")]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> VerifyProfile(
        Guid memberKey,
        [FromBody] VerifyMemberProfileRequest? request,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(
            new VerifyMemberProfileCommand(memberKey, request?.Note),
            cancellationToken);

        return response.ToActionResult(this);
    }

    /// <summary>
    /// Withdraws that check, sending the profile back for review.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireGroupLeadership)]
    [HttpDelete("{memberKey:guid}/profile-verification")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Update, "route:memberKey")]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResetProfileVerification(
        Guid memberKey,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(
            new ResetMemberProfileVerificationCommand(memberKey),
            cancellationToken);

        return response.ToActionResult(this);
    }

    /// <summary>
    /// Deletes a member.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("{memberKey:guid}")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Delete, "route:memberKey")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(Guid memberKey)
    {
        var command = new DeleteMemberCommand(memberKey);
        var response = await _mediator.Send(command);
        return response.ToActionResult(this);
    }
}
