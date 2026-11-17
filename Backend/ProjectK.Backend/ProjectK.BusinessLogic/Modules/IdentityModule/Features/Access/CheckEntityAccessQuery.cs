using MediatR;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Access;

public sealed class CheckEntityAccessQuery : IRequest<ServiceResult<bool>>
{
    public string EntityType { get; set; }
    public string EntityKey { get; set; }
    public string? Action { get; set; }

    // Deprecated: ignored by handler. Kept only for compatibility.
    public string? ActiveKurinKey { get; set; }
}

public sealed class CheckEntityAccessQueryHandler : IRequestHandler<CheckEntityAccessQuery, ServiceResult<bool>>
{
    private readonly IResourceAccessService _resourceAccessService;

    public CheckEntityAccessQueryHandler(IResourceAccessService resourceAccessService)
    {
        _resourceAccessService = resourceAccessService;
    }

    public async Task<ServiceResult<bool>> Handle(CheckEntityAccessQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.EntityKey, out var parsedEntityKey))
        {
            return ServiceResult<bool>.Failure(ResultType.BadRequest, IdentityErrors.InvalidEntityKey, "Invalid entity key.");
        }

        if (!TryMapResourceType(request.EntityType, out var resourceType))
        {
            return ServiceResult<bool>.Failure(ResultType.BadRequest, IdentityErrors.InvalidEntityType, "Invalid entity type.");
        }

        if (!TryResolveResourceAction(request.Action, out var action, out var actionError))
        {
            return ServiceResult<bool>.Failure(ResultType.BadRequest, IdentityErrors.InvalidResourceAction, actionError);
        }

        var decision = await _resourceAccessService.CheckAccessAsync(
            resourceType,
            action,
            parsedEntityKey,
            cancellationToken);

        return new ServiceResult<bool>(ResultType.Success, decision.IsAllowed);
    }

    private static bool TryMapResourceType(string entityType, out ResourceType resourceType)
    {
        if (Enum.TryParse<ResourceType>(entityType, ignoreCase: true, out resourceType))
        {
            return true;
        }

        return entityType.ToLowerInvariant() switch
        {
            "group" => Map(ResourceType.Group, out resourceType),
            "member" => Map(ResourceType.Member, out resourceType),
            "kurin" => Map(ResourceType.Kurin, out resourceType),
            "kv" => Map(ResourceType.Kurin, out resourceType),
            _ => Map(default, out resourceType, false)
        };
    }

    private static bool Map(ResourceType type, out ResourceType mapped, bool result = true)
    {
        mapped = type;
        return result;
    }

    private static bool TryResolveResourceAction(string? actionValue, out ResourceAction action, out string error)
    {
        if (string.IsNullOrWhiteSpace(actionValue))
        {
            action = ResourceAction.Read;
            error = string.Empty;
            return true;
        }

        if (Enum.TryParse<ResourceAction>(actionValue, ignoreCase: true, out action))
        {
            error = string.Empty;
            return true;
        }

        action = ResourceAction.Read;
        error = "Invalid resource action.";
        return false;
    }
}
