namespace ProjectK.Common.Dtos.IdentityModule;

public class JwtResponse
{
    public string AccessToken { get; set; } = null!;
    public RefreshToken RefreshToken { get; set; } = null!;
}
