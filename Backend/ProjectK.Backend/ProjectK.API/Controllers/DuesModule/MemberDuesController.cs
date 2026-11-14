using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Filters;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Member;
using ProjectK.Common.Dtos.DuesModule;
using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.API.Controllers.DuesModule;

/// <summary>
/// One person's вкладка. The permission is <c>GroupDues:Read</c>, but the scope is read from the
/// person: a youth holds it at <c>Own</c> and so sees their own balance and nobody else's.
/// </summary>
[ApiController]
[Route("api/member/{memberKey:guid}/dues")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class MemberDuesController : ControllerBase
{
    private readonly IMediator _mediator;

    public MemberDuesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ResourceAuthorize(ResourceType.GroupDues, ResourceAction.Read, "route:memberKey", ResourceType.Member)]
    [ProducesResponseType(typeof(MemberDuesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid memberKey)
    {
        var result = await _mediator.Send(new GetMemberDuesQuery(memberKey));
        return result.ToActionResult(this);
    }
}
