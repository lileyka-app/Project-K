using System.Text.Json.Serialization;

namespace ProjectK.Common.Models.Dtos.AuthModule.Requests;

/// <summary>Body of <c>POST api/auth/accounts/switch</c>: the kept account to make the one in use.</summary>
public sealed class SwitchAccountRequest
{
    [JsonRequired]
    public Guid UserKey { get; set; }
}
