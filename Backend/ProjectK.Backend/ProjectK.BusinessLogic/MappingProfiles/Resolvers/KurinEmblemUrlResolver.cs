using AutoMapper;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;

namespace ProjectK.BusinessLogic.MappingProfiles.Resolvers;

public sealed class KurinEmblemUrlResolver : IValueResolver<Kurin, KurinResponse, string?>
{
    private readonly IBlobReadLinks _links;

    public KurinEmblemUrlResolver(IBlobReadLinks links)
    {
        _links = links;
    }

    public string? Resolve(Kurin source, KurinResponse destination, string? destMember, ResolutionContext context)
        => _links.For(source.EmblemBlobName);
}
