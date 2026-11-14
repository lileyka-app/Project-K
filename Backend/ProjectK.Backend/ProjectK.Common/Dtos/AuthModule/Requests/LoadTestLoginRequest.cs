namespace ProjectK.Common.Dtos.AuthModule.Requests;

/// <summary>Body of <c>POST api/auth/loadtest-login</c>.</summary>
public sealed class LoadTestLoginRequest
{
    public string ApiKey { get; set; } = string.Empty;
}
