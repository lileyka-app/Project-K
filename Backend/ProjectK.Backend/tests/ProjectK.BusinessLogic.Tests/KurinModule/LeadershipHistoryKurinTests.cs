using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using ProjectK.BusinessLogic.MappingProfiles;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule;

/// <summary>
/// A card carries offices from every kurin the person stands in; each says which kurin it is in,
/// so the card shows it there only and names it by that kurin's own role names.
/// </summary>
public class LeadershipHistoryKurinTests
{
    private readonly IMapper _mapper = new MapperConfiguration(cfg =>
    {
        cfg.ConstructServicesUsing(FakeBlobReadLinks.Resolvers);
        cfg.AddProfile(new KurinModuleProfile());
    }, LoggerFactory.Create(_ => { })).CreateMapper();

    private static LeadershipHistory HeldIn(Leadership office) => new()
    {
        LeadershipHistoryKey = Guid.NewGuid(),
        LeadershipKey = office.LeadershipKey,
        Leadership = office,
        Role = LeadershipRole.Pysar,
        StartDate = new DateOnly(2025, 9, 1)
    };

    [Fact]
    public void AKurinOffice_SaysItsOwnKurin()
    {
        var kurinKey = Guid.NewGuid();
        var office = new Leadership { LeadershipKey = Guid.NewGuid(), Type = LeadershipType.Kurin, KurinKey = kurinKey };

        _mapper.Map<LeadershipHistoryDto>(HeldIn(office)).KurinKey.Should().Be(kurinKey);
    }

    [Fact]
    public void AGroupOffice_SaysTheKurinItsGroupIsIn()
    {
        var kurinKey = Guid.NewGuid();
        var group = new Group("Соколи", kurinKey) { GroupKey = Guid.NewGuid() };
        var office = new Leadership { LeadershipKey = Guid.NewGuid(), Type = LeadershipType.Group, GroupKey = group.GroupKey, Group = group };

        _mapper.Map<LeadershipHistoryDto>(HeldIn(office)).KurinKey.Should().Be(kurinKey);
    }
}
