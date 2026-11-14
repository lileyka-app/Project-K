using MediatR;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Access;

public class CheckEntityAccessQuery : IRequest<ServiceResult<bool>>
{
    public string EntityType { get; set; }
    public string EntityKey { get; set; }
    public string? Action { get; set; }

    // Deprecated: ignored by handler. Kept only for compatibility.
    public string? ActiveKurinKey { get; set; }
}

public class CheckEntityAccessQueryHandler : IRequestHandler<CheckEntityAccessQuery, ServiceResult<bool>>
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
            return ServiceResult<bool>.Failure(ResultType.BadRequest, AuthErrors.InvalidEntityKey, "Invalid entity key.");
        }

        if (!TryMapResourceType(request.EntityType, out var resourceType))
        {
            return ServiceResult<bool>.Failure(ResultType.BadRequest, AuthErrors.InvalidEntityType, "Invalid entity type.");
        }

        if (!TryResolveResourceAction(request.Action, out var action, out var actionError))
        {
            return ServiceResult<bool>.Failure(ResultType.BadRequest, AuthErrors.InvalidResourceAction, actionError);
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
