using System.ComponentModel.DataAnnotations;

namespace ProjectK.Common.Dtos.KurinModule.Requests;

public class CreateGroupRequest
{
    public string Name { get; set; }
    public Guid KurinKey { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}
