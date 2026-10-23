using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Helpers;
using ProjectK.API.Models.Requests;
using ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Import;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.AccessPolicy;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Delete;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.ExportReportPdf;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.SetKrai;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.Former;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.Join;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.Leave;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.Lookup;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.MoveToGroup;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Registry.Export;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge.Get;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.KraiProfiles;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Reports;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>
/// The kurin itself: its record, its badge review queue, and its report.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class KurinController : ControllerBase
{
    /// <summary>
    /// A kurin's roster is a few dozen rows. Five megabytes is far more than that could ever be,
    /// and is here so a wrong file is refused at the door rather than read into memory.
    /// </summary>
    private const long MaxRosterBytes = 5 * 1024 * 1024;

    private readonly IMediator _mediator;

    public KurinController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lists badge submissions across the kurin that are waiting on a decision.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireGroupLeadership)]
    [HttpGet("{kurinKey:guid}/badges/review")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(IEnumerable<BadgeProgressResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBadgeReviewQueue(Guid kurinKey)
    {
        var response = await _mediator.Send(new GetBadgeReviewQueueQuery(kurinKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns one kurin.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey}")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByKey(Guid kurinKey)
    {
        var request = new GetKurinByKeyQuery(kurinKey);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Reads an uploaded roster and answers with its columns, a guess at what each one means, and
    /// every data row. Writes nothing.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpPost("{kurinKey:guid}/import/preview")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [RequestSizeLimit(MaxRosterBytes)]
    [ProducesResponseType(typeof(RosterPreview), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewRosterImport(
        Guid kurinKey,
        [FromForm] UploadRosterRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return this.Failure(ResultType.BadRequest, "NoFile", "No file was uploaded.");
        }

        await using var content = request.File.OpenReadStream();
        var result = await _mediator.Send(new PreviewRosterImportQuery(content), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Applies a mapped roster to the kurin — or, with <c>dryRun</c>, only reports what it would do.
    /// </summary>
    /// <remarks>
    /// One transaction: either every usable row lands or none does. A half-imported kurin is worse
    /// than an unimported one, because nobody can tell which half is missing.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpPost("{kurinKey:guid}/import")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(RosterImportReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ImportRoster(
        Guid kurinKey,
        [FromBody] ApplyRosterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ImportRosterCommand(
                kurinKey,
                request.Rows,
                request.Mapping,
                request.CreateMissingGroups,
                request.DryRun),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// The kurin's registry as a spreadsheet, in the columns the caller asked for.
    /// </summary>
    /// <remarks>
    /// Answered from the same read the screen uses, so a field the caller may not see on screen is
    /// not in the file either. Nothing is exported that could not simply be looked at.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireGroupLeadership)]
    [HttpPost("{kurinKey:guid}/registry/export")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportRegistry(
        Guid kurinKey,
        [FromBody] ExportRegistryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ExportRegistryQuery(kurinKey, request.Columns ?? []),
            cancellationToken);

        if (result.Type != ResultType.Success || result.Data is null)
        {
            return result.ToActionResult(this);
        }

        return File(
            result.Data.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            result.Data.FileName);
    }

    /// <summary>
    /// Renders the kurin's report as a PDF.
    /// </summary>
    /// <remarks>
    /// The heaviest read in the API — it walks membership, offices, probes and badges to compose one
    /// document.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpGet("{kurinKey:guid}/report/pdf")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/pdf")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    /// <param name="timeZone">
    /// The reader's IANA zone, so the document prints the hours their own clock showed. Only the
    /// browser knows it, so it says. Absent or unknown prints UTC.
    /// </param>
    public async Task<IActionResult> ExportReportPdf(
        Guid kurinKey,
        [FromQuery] string? timeZone,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ExportKurinReportPdfQuery(kurinKey, timeZone), cancellationToken);
        if (response.Type != ResultType.Success || response.Data is null)
        {
            return response.ToActionResult(this);
        }

        return File(response.Data.Content, "application/pdf", response.Data.FileName);
    }

    /// <summary>
    /// Lists every kurin. Administrators only, since nobody else works above one kurin.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpGet("kurins")]
    [ProducesResponseType(typeof(IEnumerable<KurinResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll()
    {
        var request = new GetKurinsQuery();
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Creates a kurin from its number and, optionally, its край (the instance default otherwise).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost]
    [ProducesResponseType(typeof(KurinResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create([FromBody] CreateKurinRequest body)
    {
        var request = new UpsertKurinCommand(body.Number, body.KraiCode);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns the край profile the kurin works under: which вмілості and проби it sees, its
    /// currency and time zone. Every value is filled in, from the край's profile or from code.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey}/profile")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(KraiProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(Guid kurinKey)
    {
        var response = await _mediator.Send(new GetKurinProfileQuery(kurinKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// The card fields the kurin may leave off: what its край makes optional or adds without
    /// requiring. Each says whether the kurin hides it.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey}/card-fields")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(IReadOnlyList<KurinCardFieldResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCardFields(Guid kurinKey)
    {
        var response = await _mediator.Send(new GetKurinCardFieldsQuery(kurinKey));
        return response.ToActionResult(this);
    }

    /// <summary>Sets which of those fields the kurin hides; a field it may not hide is ignored.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("{kurinKey}/card-fields")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(IReadOnlyList<KurinCardFieldResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetCardFields(Guid kurinKey, [FromBody] SetKurinCardFieldsRequest body)
    {
        var response = await _mediator.Send(new SetKurinCardFieldsCommand(kurinKey, body.Hidden ?? []));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// The kurin's «Права й видимість»: for every capability and audience, what the kurin has now,
    /// what the default is, and whether the cell is locked.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey:guid}/access-policy")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinAccessPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccessPolicy(Guid kurinKey, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetKurinAccessPolicyQuery(kurinKey), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Saves the kurin's policy from the whole effective matrix. Only what differs from the defaults
    /// is kept; an unknown name or a change to a locked cell is refused with <c>AccessPolicyInvalid</c>.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("{kurinKey:guid}/access-policy")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinAccessPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAccessPolicy(
        Guid kurinKey,
        [FromBody] SetKurinAccessPolicyRequest body,
        CancellationToken cancellationToken)
    {
        var grants = (body.Grants ?? []).ToDictionary(
            row => row.Key,
            row => (IReadOnlyDictionary<string, string?>)(row.Value ?? []));

        var response = await _mediator.Send(new SetKurinAccessPolicyCommand(kurinKey, grants), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>Body of <c>PUT {kurinKey}/card-fields</c>.</summary>
    public sealed class SetKurinCardFieldsRequest
    {
        public List<string>? Hidden { get; set; }
    }

    /// <summary>
    /// Moves a kurin to another край. Admin-only; the number must be free in that край.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPut("{kurinKey}/krai")]
    [ProducesResponseType(typeof(KurinResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetKrai(Guid kurinKey, [FromBody] SetKurinKraiRequest body)
    {
        var response = await _mediator.Send(new SetKurinKraiCommand(kurinKey, body.KraiCode));
        return response.ToActionResult(this);
    }

    /// <summary>Body of <c>POST api/kurin</c>.</summary>
    public sealed class CreateKurinRequest
    {
        public int Number { get; set; }
        public string? KraiCode { get; set; }
    }

    /// <summary>Body of <c>PUT {kurinKey}/krai</c>.</summary>
    public sealed class SetKurinKraiRequest
    {
        public string KraiCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Rewrites a kurin's record.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("{kurinKey}")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upsert(Guid kurinKey, [FromBody] UpdateKurinRequest request)
    {
        var command = new UpsertKurinCommand(
            kurinKey,
            request.Number,
            request.Stanytsia,
            request.RegionOrCountry,
            request.NamedAfter,
            request.Description,
            request.ProfileVerificationEnabled);
        var response = await _mediator.Send(command);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Deletes a kurin.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("{kurinKey}")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Delete, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(Guid kurinKey)
    {
        var request = new DeleteKurinCommand(kurinKey);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Body of <c>POST {kurinKey}/memberships</c>. Name the person by key when they are already
    /// on a screen in this kurin, or by their public code when they are not.
    /// </summary>
    public sealed class JoinKurinRequest
    {
        public Guid MemberKey { get; set; }
        public string? PublicId { get; set; }
        public Guid? GroupKey { get; set; }
        public MembershipKind Kind { get; set; } = MembershipKind.Youth;
    }

    /// <summary>Body of <c>PUT {kurinKey}/memberships/{memberKey}/group</c>.</summary>
    public sealed class MoveToGroupRequest
    {
        public Guid? GroupKey { get; set; }
    }

    /// <summary>
    /// Takes a person into this kurin. They keep every other kurin they belong to and everything
    /// they have earned anywhere — this adds a membership, it does not move them.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost("{kurinKey:guid}/memberships")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Create, "route:kurinKey", ResourceType.Kurin)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Join(Guid kurinKey, [FromBody] JoinKurinRequest request)
    {
        var response = await _mediator.Send(
            new JoinKurinCommand(request.MemberKey, kurinKey, request.GroupKey, request.Kind, request.PublicId));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Looks up the person a public code names, so the провід can be sure before taking them in.
    /// </summary>
    /// <remarks>
    /// An exact match on a code someone handed over — not a search. The card carries no kurins
    /// and no contact details: answering "where else is this person" to anyone holding a code
    /// would tell more about them than they agreed to.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey:guid}/memberships/candidate")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Create, "route:kurinKey", ResourceType.Kurin)]
    [ProducesResponseType(typeof(MembershipCandidate), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindCandidate(Guid kurinKey, [FromQuery] string publicId)
    {
        var response = await _mediator.Send(new FindMemberByPublicIdQuery(kurinKey, publicId));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Who used to belong to this kurin, so the провід can see whom it let go — and take any of
    /// them back with <c>POST {kurinKey}/memberships</c>, without needing the code they hold.
    /// </summary>
    /// <remarks>
    /// Read against this kurin's own memberships. It answers "who was here and when did they
    /// leave" and nothing else: not where they are now, not how to reach them.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey:guid}/memberships/former")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Create, "route:kurinKey", ResourceType.Kurin)]
    [ProducesResponseType(typeof(IReadOnlyCollection<FormerMember>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FormerMembers(Guid kurinKey)
    {
        var response = await _mediator.Send(new GetFormerMembersQuery(kurinKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Ends a person's membership in this kurin. Their record and their history stay where they
    /// are; only the belonging is closed.
    /// </summary>
    /// <remarks>
    /// Only the Звʼязковий closes a membership: a youth does not leave on their own, and a
    /// Виховник does not release someone from the kurin.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpDelete("{kurinKey:guid}/memberships/{memberKey:guid}")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Delete, "route:memberKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Leave(Guid kurinKey, Guid memberKey)
    {
        var response = await _mediator.Send(new LeaveKurinCommand(memberKey, kurinKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Moves a person between гуртки of this kurin, or out of one — a null гурток is allowed.
    /// </summary>
    /// <remarks>
    /// Placement is the Звʼязковий's, like closing a membership. <c>Member:Update</c> is not enough:
    /// everyone holds it on their own record and a Виховник on his гурток, so either could move
    /// themselves, or someone else, into a гурток they chose.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpPut("{kurinKey:guid}/memberships/{memberKey:guid}/group")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Manage, "route:memberKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveToGroup(Guid kurinKey, Guid memberKey, [FromBody] MoveToGroupRequest request)
    {
        var response = await _mediator.Send(new MoveToGroupCommand(memberKey, kurinKey, request.GroupKey));
        return response.ToActionResult(this);
    }
}
