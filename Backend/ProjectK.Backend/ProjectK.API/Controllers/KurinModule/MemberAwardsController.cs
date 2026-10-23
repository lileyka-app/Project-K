using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Helpers;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Delete;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Review;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Upsert;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.ProbesAndBadgesModule.Requests;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.KurinModule;

[ApiController]
[Route("api/member/{memberKey:guid}/awards")]
// Says what it means: authenticated. The scope check is the ResourceAuthorize filter on each action.
/// <summary>
/// Awards recorded against a member, and the images that illustrate them.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class MemberAwardsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAwardImagesCatalog _awardImages;

    public MemberAwardsController(IMediator mediator, IAwardImagesCatalog awardImages)
    {
        _mediator = mediator;
        _awardImages = awardImages;
    }

    /// <summary>
    /// Records an award for a member, or rewrites one already recorded.
    /// </summary>
    [HttpPost]
    [ResourceAuthorize(ResourceType.MemberAward, ResourceAction.Create, "route:memberKey", ResourceType.Member)]
    [ProducesResponseType(typeof(MemberAwardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertAward(Guid memberKey, [FromBody] UpsertMemberAwardCommand command)
    {
        command.MemberKey = memberKey;
        var result = await _mediator.Send(command);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Confirms or refuses a recorded award.
    /// </summary>
    /// <remarks>
    /// The resource check admits whoever confirms awards of this member at all; which levels they
    /// may confirm is the kurin's policy, checked by the handler.
    /// </remarks>
    [HttpPost("{awardKey:guid}/review")]
    [ResourceAuthorize(ResourceType.MemberAward, ResourceAction.Update, "route:memberKey", ResourceType.Member)]
    [ProducesResponseType(typeof(MemberAwardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReviewAward(Guid memberKey, Guid awardKey, [FromBody] ReviewBadgeProgressRequest request)
    {
        var result = await _mediator.Send(new ReviewMemberAwardCommand
        {
            MemberKey = memberKey,
            MemberAwardKey = awardKey,
            IsApproved = request.IsApproved
        });
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Removes an award.
    /// </summary>
    /// <remarks>
    /// An unconfirmed award can be withdrawn by the member it is about or by whoever submitted it. A
    /// confirmed one, or someone else's submission, takes the kurin's «remove awards» right — so
    /// the endpoint lets in whoever may see the member and the handler decides.
    /// </remarks>
    [HttpDelete("{awardKey:guid}")]
    [ResourceAuthorize(ResourceType.MemberAward, ResourceAction.Read, "route:memberKey", ResourceType.Member)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAward(Guid memberKey, Guid awardKey)
    {
        var result = await _mediator.Send(new DeleteMemberAwardCommand { MemberKey = memberKey, MemberAwardKey = awardKey });
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Serves the illustration for an award level, coloured or plain, as the catalogue publishes it.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("/api/awards/images/{level}")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "image/png")]
    public async Task<IActionResult> GetAwardImage(int level, [FromQuery] bool colored = true, CancellationToken cancellationToken = default)
    {
        var image = await _awardImages.GetAwardImageAsync(level, colored, cancellationToken);
        if (image == null)
        {
            return this.Failure(ResultType.NotFound, "AwardImageNotFound", "No award image exists for this level.");
        }

        Response.Headers.CacheControl = "public,max-age=86400";
        return File(image.Content, image.ContentType);
    }
}
