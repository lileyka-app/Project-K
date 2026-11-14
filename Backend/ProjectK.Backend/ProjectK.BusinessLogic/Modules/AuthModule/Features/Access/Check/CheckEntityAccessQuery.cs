using MediatR;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Access.Check;

public class CheckEntityAccessQuery : IRequest<ServiceResult<bool>>
{
    public string EntityType { get; set; }
    public string EntityKey { get; set; }
    public string? Action { get; set; }

    // Deprecated: ignored by handler. Kept only for compatibility.
    public string? ActiveKurinKey { get; set; }
}
