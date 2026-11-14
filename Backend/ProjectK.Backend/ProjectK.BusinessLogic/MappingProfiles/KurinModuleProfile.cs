using AutoMapper;
using ProjectK.BusinessLogic.MappingProfiles.Resolvers;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.PlanningSession.Create;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Entities.InfrastructureModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.KurinModule.Planning;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.MappingProfiles;

public class KurinModuleProfile : Profile
{
    public KurinModuleProfile()
    {
        // Kurin Mapping
        CreateMap<Kurin, KurinResponse>()
            .ForMember(dest => dest.CurrentUserCount, opt => opt.MapFrom(src => src.Memberships.Count(ms => ms.LeftAtUtc == null)))
            .ForMember(dest => dest.Branches, opt => opt.MapFrom(src => src.Numbering))
            .ForMember(dest => dest.ScoreEnabled, opt => opt.MapFrom(src => src.IsScoreEnabled))
            .ForMember(dest => dest.RoleNames, opt => opt.MapFrom(src => KurinRoleNames.Read(src.RoleNames).ToDictionary(pair => pair.Key.ToString(), pair => pair.Value)))
            .ForMember(dest => dest.CanRenameRoles, opt => opt.MapFrom(src => KurinRoleNames.IsAvailableFor(src.Branch)))
            .ForMember(dest => dest.EmblemUrl, opt => opt.MapFrom<KurinEmblemUrlResolver>());
        CreateMap<UpsertKurinCommand, Kurin>(MemberList.None)
            .ForMember(dest => dest.KurinKey, opt => opt.Ignore())
            .ForMember(dest => dest.Number, opt => opt.Ignore())
            .ForMember(dest => dest.Branch, opt => opt.Ignore())
            .ForMember(dest => dest.Branches, opt => opt.Ignore())
            .ForMember(dest => dest.ScoreEnabled, opt => opt.Ignore())
            .ForMember(dest => dest.RoleNames, opt => opt.Ignore())
            .ForMember(dest => dest.EmblemBlobName, opt => opt.Ignore())
            .ForMember(dest => dest.KraiCode, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedDate, opt => opt.MapFrom(src => DateTime.UtcNow));

        // Group Mapping
        CreateMap<Group, GroupResponse>()
            .ForMember(dest => dest.KurinNumber, opt => opt.MapFrom(src => src.Kurin.Number))
            .ForMember(dest => dest.SilhouetteUrl, opt => opt.MapFrom<GroupSilhouetteUrlResolver>());
        CreateMap<UpsertGroupCommand, Group>(MemberList.None)
            .ForMember(dest => dest.GroupKey, opt => opt.Ignore())
            // A гурток does not change kurin by being renamed. The update path builds the command
            // without a KurinKey, so mapping it would write Guid.Empty over a live foreign key.
            .ForMember(dest => dest.KurinKey, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedDate, opt => opt.MapFrom(src => DateTime.UtcNow));

        // Notifications Mapping
        CreateMap<AppNotification, AppNotificationDto>();

        // Planning Mapping
        CreateMap<CreatePlanningSessionCommand, PlanningSession>(MemberList.None);

        CreateMap<ParticipantInputDto, PlanningParticipant>(MemberList.None);

        CreateMap<DateRangeDto, ParticipantBusyRange>(MemberList.None);

        // Entity -> Response DTO
        // CanDelete is about the caller, not the record, so the handlers set it after mapping.
        CreateMap<PlanningSession, PlanningSessionResponse>()
            .ForMember(response => response.CanDelete, options => options.Ignore());
        CreateMap<PlanningParticipant, PlanningParticipantDto>();
        CreateMap<ParticipantBusyRange, DateRangeDto>();
    }
}
