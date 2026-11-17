namespace ProjectK.Common.Dtos.IdentityModule.Requests;

/// <summary>Body of <c>POST api/auth/kurin-scope</c>: the kurin to act in, or none.</summary>
public sealed class SetKurinScopeRequest
{
    public Guid? KurinKey { get; set; }
}
