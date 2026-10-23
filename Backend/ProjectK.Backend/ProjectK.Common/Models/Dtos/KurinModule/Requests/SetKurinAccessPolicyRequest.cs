namespace ProjectK.Common.Models.Dtos.KurinModule.Requests;

/// <summary>
/// Body of <c>PUT api/kurin/{kurinKey}/access-policy</c>: the whole effective matrix, capability →
/// audience → reach, all by name. Kept as strings so a name the server does not know is answered
/// with <c>AccessPolicyInvalid</c> rather than a binding error.
/// </summary>
public sealed class SetKurinAccessPolicyRequest
{
    public Dictionary<string, Dictionary<string, string?>?>? Grants { get; set; }
}
