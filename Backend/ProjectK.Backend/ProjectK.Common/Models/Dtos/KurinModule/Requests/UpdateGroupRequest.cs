using System.ComponentModel.DataAnnotations;

namespace ProjectK.Common.Models.Dtos.KurinModule.Requests;

public class UpdateGroupRequest
{
    public string Name { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}
