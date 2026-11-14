using System.Security.Claims;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.API.Hosting;

public sealed class HttpCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId => Principal.GetUserKey();

    public Guid? KurinKey => ParseGuid(Principal?.FindFirstValue(AccessTokenClaims.KurinKey));

    public IReadOnlyCollection<string> Roles => ResolveRoles();

    public bool IsInRole(string role)
    {
        return Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private static Guid? ParseGuid(string? value)
    {
        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    private IReadOnlyCollection<string> ResolveRoles()
    {
        return Principal?
            .FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
    }
}
