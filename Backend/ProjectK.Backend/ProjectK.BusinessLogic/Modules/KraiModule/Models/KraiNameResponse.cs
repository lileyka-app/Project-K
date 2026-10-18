namespace ProjectK.BusinessLogic.Modules.KraiModule.Models;

/// <summary>A край as a form offers it.</summary>
public sealed class KraiNameResponse
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>The край a kurin opens in when nobody picks one (<c>Krai:Default</c>).</summary>
    public bool IsDefault { get; set; }
}
