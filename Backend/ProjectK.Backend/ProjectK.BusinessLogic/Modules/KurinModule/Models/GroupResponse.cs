namespace ProjectK.BusinessLogic.Modules.KurinModule.Models;

public class GroupResponse
{
    public Guid GroupKey { get; set; }
    public Guid KurinKey { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? SilhouetteUrl { get; set; }
    public int? KurinNumber { get; set; }
}
