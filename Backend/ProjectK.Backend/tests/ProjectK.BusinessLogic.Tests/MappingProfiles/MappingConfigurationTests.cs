using AutoMapper;
using AutoMapper.EquivalencyExpression;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectK.BusinessLogic.MappingProfiles;

namespace ProjectK.BusinessLogic.Tests.MappingProfiles;

/// <summary>
/// AutoMapper fills what it can match by name and leaves the rest at its default, so a renamed or
/// added property silently arrives empty. Nothing guarded the twenty-six existing maps against that
/// until the hand-written mappers were folded in here — this check is what makes standardising on the
/// reflection-based approach safe.
/// </summary>
public class MappingConfigurationTests
{
    [Fact]
    public void EveryProfile_MapsEveryDestinationMember()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddCollectionMappers();
            cfg.AddProfile(new AuthModuleProfile());
            cfg.AddProfile(new KurinModuleProfile());
        }, NullLoggerFactory.Instance);

        configuration.AssertConfigurationIsValid();
    }
}
