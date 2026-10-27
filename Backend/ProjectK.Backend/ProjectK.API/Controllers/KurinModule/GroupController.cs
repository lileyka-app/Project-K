using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Helpers;
using ProjectK.API.Models.Requests;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.Delete;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.ProbeEdition;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.Silhouette;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment.Assign;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment.Revoke;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>
/// Groups within a kurin, and the mentors assigned to them.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class GroupController : ControllerBase
{
    private readonly IMediator _mediator;

    public GroupController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns one group.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{groupKey}")]
    [ResourceAuthorize(ResourceType.Group, ResourceAction.Read, "route:groupKey")]
    [ProducesResponseType(typeof(GroupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByKey(Guid groupKey)
    {
        var request = new GetGroupByKeyQuery(groupKey);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Answers whether a group exists, without returning it.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("exists/{groupKey}")]
    [ResourceAuthorize(ResourceType.Group, ResourceAction.Read, "route:groupKey")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Exists(Guid groupKey)
    {
        var request = new ExistsGroupByKeyQuery(groupKey);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Lists the groups of one kurin.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("groups")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "query:kurinKey")]
    [ProducesResponseType(typeof(IEnumerable<GroupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(Guid kurinKey)
    {
        var request = new GetGroupsQuery(kurinKey);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Creates a group.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Create, "arg:request.KurinKey")]
    [ProducesResponseType(typeof(GroupResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create([FromBody] CreateGroupRequest request)
    {
        var command = new UpsertGroupCommand(request.Name, request.KurinKey, request.Description);
        var response = await _mediator.Send(command);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Rewrites a group.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("{groupKey:guid}")]
    [ResourceAuthorize(ResourceType.Group, ResourceAction.Update, "route:groupKey")]
    [ProducesResponseType(typeof(GroupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(Guid groupKey, [FromBody] UpdateGroupRequest request)
    {
        var command = new UpsertGroupCommand(groupKey, request.Name, request.Description);
        var response = await _mediator.Send(command);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Moves the group to another edition of the probe programme.
    /// </summary>
    /// <remarks>
    /// Never refused for who is in the group: a probe under way stays in its edition. Only an
    /// edition the catalogue offers the kurin's край is accepted (400 <c>ProbeEditionNotOffered</c>).
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("{groupKey:guid}/probe-edition")]
    [ResourceAuthorize(ResourceType.GroupProbeEdition, ResourceAction.Update, "route:groupKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetProbeEdition(Guid groupKey, [FromBody] SetGroupProbeEditionRequest request)
    {
        var response = await _mediator.Send(new SetGroupProbeEditionCommand(groupKey, request.Edition));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Stores the group's silhouette image.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost("{groupKey:guid}/silhouette")]
    [ResourceAuthorize(ResourceType.Group, ResourceAction.Update, "route:groupKey")]
    [RequestSizeLimit(ImageUploadRules.MaxRequestBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(GroupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadSilhouette(Guid groupKey, [FromForm] UploadImageRequest form, CancellationToken cancellationToken)
    {
        var file = form.File;
        if (file == null || file.Length == 0)
        {
            return this.Failure(ResultType.BadRequest, "MissingImage", "Image file is required.");
        }

        if (ImageUploadRules.Refusal(file) is { } refusal)
        {
            return this.Failure(ResultType.BadRequest, refusal.Code, refusal.Message);
        }

        var bytes = await file.ToByteArrayAsync(cancellationToken);
        if (bytes == null || bytes.Length == 0)
        {
            return this.Failure(ResultType.BadRequest, "MissingImage", "Image file is required.");
        }

        var command = new UploadGroupSilhouetteCommand(groupKey, bytes, file.FileName);
        var response = await _mediator.Send(command, cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Removes the group's silhouette image.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("{groupKey:guid}/silhouette")]
    [ResourceAuthorize(ResourceType.Group, ResourceAction.Update, "route:groupKey")]
    [ProducesResponseType(typeof(GroupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSilhouette(Guid groupKey, CancellationToken cancellationToken)
    {
        var command = new DeleteGroupSilhouetteCommand(groupKey);
        var response = await _mediator.Send(command, cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Deletes a group.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("{groupKey}")]
    [ResourceAuthorize(ResourceType.Group, ResourceAction.Delete, "route:groupKey")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(Guid groupKey)
    {
        var command = new DeleteGroupCommand(groupKey);
        var response = await _mediator.Send(command);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Lists the mentors assigned to a group.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{groupKey}/mentors")]
    [ResourceAuthorize(ResourceType.Group, ResourceAction.Read, "route:groupKey")]
    [ProducesResponseType(typeof(IEnumerable<MemberLookupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMentors(Guid groupKey)
    {
        var query = new GetGroupMentorsQuery(groupKey);
        var response = await _mediator.Send(query);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Lists mentor assignments across the whole kurin.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("groups/{kurinKey}/mentor-assignments")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(IEnumerable<MentorAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKurinMentorAssignments(Guid kurinKey)
    {
        var query = new GetKurinMentorAssignmentsQuery(kurinKey);
        var response = await _mediator.Send(query);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Assigns a mentor to a group.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost("{groupKey}/mentors/{mentorUserKey}")]
    [ResourceAuthorize(ResourceType.Group, ResourceAction.Manage, "route:groupKey")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignMentor(Guid groupKey, Guid mentorUserKey)
    {
        var command = new AssignMentorCommand(mentorUserKey, groupKey);
        var response = await _mediator.Send(command);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Removes a mentor's assignment to a group.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("{groupKey}/mentors/{mentorUserKey}")]
    [ResourceAuthorize(ResourceType.Group, ResourceAction.Manage, "route:groupKey")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeMentor(Guid groupKey, Guid mentorUserKey)
    {
        var command = new RevokeMentorCommand(mentorUserKey, groupKey);
        var response = await _mediator.Send(command);
        return response.ToActionResult(this);
    }
}
