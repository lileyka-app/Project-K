namespace ProjectK.Common.Models.Dtos.AuthModule;

public class JwtResponse
{
    public string AccessToken { get; set; } = null!;
    public RefreshToken RefreshToken { get; set; } = null!;
}
