using AutoMapper;
using AutoMapper.EquivalencyExpression;
using ProjectK.BusinessLogic.MappingProfiles.Resolvers;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.PlanningSession.Create;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Entities.InfrastructureModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.KurinModule.Planning;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.KraiProfiles;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.MappingProfiles;

public class KurinModuleProfile : Profile
{
    public KurinModuleProfile()
    {
        // Kurin Mapping
        CreateMap<Kurin, KurinResponse>()
            .ForMember(dest => dest.CurrentUserCount, opt => opt.MapFrom(src => src.Memberships.Count(ms => ms.LeftAtUtc == null)));
        CreateMap<UpsertKurinCommand, Kurin>(MemberList.None)
            .ForMember(dest => dest.KurinKey, opt => opt.Ignore())
            .ForMember(dest => dest.Number, opt => opt.Ignore())
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

        // Member Mapping
        CreateMap<UpsertMemberProfileCommand, Member>(MemberList.None)
            .ForMember(dest => dest.MemberKey, opt => opt.Ignore())
            .ForMember(dest => dest.UserKey, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedDate, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.PlastLevelHistory, opt => opt.Ignore())
            .ForMember(dest => dest.CardExtras, opt => opt.Ignore());

        CreateMap<Member, MemberResponse>()
            .ForMember(dest => dest.LatestPlastLevel, opt => opt.MapFrom(src =>
                src.PlastLevelHistory
                    .OrderByDescending(history => history.DateAchieved)
                    .Select(history => (PlastLevel?)history.PlastLevel)
                    .FirstOrDefault() ?? src.LatestPlastLevel))
            .ForMember(dest => dest.PlastLevelHistories, opt => opt.MapFrom(src => src.PlastLevelHistory))
            .ForMember(dest => dest.CardExtras, opt => opt.MapFrom(src => MemberCardExtras.Read(src.CardExtras)))
            .ForMember(dest => dest.Warnings, opt => opt.MapFrom(src => src.MemberWarnings))
            .ForMember(dest => dest.Awards, opt => opt.MapFrom(src => src.MemberAwards))
            .ForMember(d => d.ProfilePhotoUrl, opt => opt.MapFrom<ProfilePhotoUrlResolver>())
            // Offices live in LeadershipHistories, not on Member, so the entity cannot answer
            // this. The repository projections fill it; mapping from the entity leaves it null.
            .ForMember(dest => dest.UserRole, opt => opt.Ignore())
            // Where the person stands — and what that гурток is called — is said by their
            // membership, which is a different table in a different aggregate. The caller fills
            // these from it; the response keeps carrying them so the frontend does not have to
            // change in the same release.
            .ForMember(dest => dest.KurinKey, opt => opt.Ignore())
            .ForMember(dest => dest.GroupKey, opt => opt.Ignore())
            .ForMember(dest => dest.GroupName, opt => opt.Ignore())
            // Standing and виховник assignments are read per placement, and this map has none.
            .ForMember(dest => dest.IsStaff, opt => opt.Ignore())
            .ForMember(dest => dest.MentoredGroupNames, opt => opt.Ignore())
            .ForMember(dest => dest.MentoredGroups, opt => opt.Ignore())
            // The account belongs to another module; the read of one person asks it.
            .ForMember(dest => dest.AccountStatus, opt => opt.Ignore())
            .ForMember(dest => dest.InvitationSent, opt => opt.Ignore())
            // Filled by MemberFieldMask, which knows who is looking.
            .ForMember(dest => dest.Birthday, opt => opt.Ignore())
            .ForMember(dest => dest.HiddenData, opt => opt.Ignore());

        // Lean list read model -> same response shape as the full card. Level,
        // active leadership and active warnings are already resolved in the
        // projection; history and awards are card-only and stay empty here.
        CreateMap<MemberListItemDto, MemberResponse>()
            // A list never carries anyone's public code: it is answered for a whole kurin, and
            // the code is something one person hands over, not something a roster hands out.
            .ForMember(dest => dest.PublicId, opt => opt.Ignore())
            .ForMember(dest => dest.CardExtras, opt => opt.Ignore())
            // Parents' contacts are read on one member's card, never with a roster.
            .ForMember(dest => dest.MotherFirstName, opt => opt.Ignore())
            .ForMember(dest => dest.MotherLastName, opt => opt.Ignore())
            .ForMember(dest => dest.MotherPhone, opt => opt.Ignore())
            .ForMember(dest => dest.FatherFirstName, opt => opt.Ignore())
            .ForMember(dest => dest.FatherLastName, opt => opt.Ignore())
            .ForMember(dest => dest.FatherPhone, opt => opt.Ignore())
            .ForMember(dest => dest.Awards, opt => opt.Ignore())
            .ForMember(dest => dest.MentoredGroups, opt => opt.Ignore())
            .ForMember(dest => dest.AccountStatus, opt => opt.Ignore())
            .ForMember(dest => dest.InvitationSent, opt => opt.Ignore())
            .ForMember(dest => dest.ProfilePhotoUrl, opt => opt.MapFrom<MemberListItemPhotoUrlResolver>());

        CreateMap<Member, MemberLookupDto>()
            .ForMember(dest => dest.UserRole, opt => opt.Ignore())
            .ForMember(dest => dest.ProfilePhotoUrl, opt => opt.MapFrom<MemberLookupPhotoUrlResolver>());

        // The mentor list is answered through IMemberDirectory, which hands back the summary
        // record rather than the entity; without this map every GET /group/{key}/mentors was a 500
        // that the unit tests, mapping with mocks, never saw.
        CreateMap<MemberSummary, MemberLookupDto>()
            .ForMember(dest => dest.MiddleName, opt => opt.Ignore())
            .ForMember(dest => dest.UserRole, opt => opt.Ignore())
            .ForMember(dest => dest.ProfileVerificationStatus, opt => opt.Ignore())
            .ForMember(dest => dest.ProfileVerifiedAtUtc, opt => opt.Ignore())
            .ForMember(dest => dest.ProfileVerifiedByUserKey, opt => opt.Ignore())
            .ForMember(dest => dest.ProfileVerificationNote, opt => opt.Ignore())
            .ForMember(dest => dest.ProfilePhotoUrl, opt => opt.MapFrom<MemberSummaryLookupPhotoUrlResolver>());

        // Plast Level History Mapping
        CreateMap<PlastLevelHistory, PlastLevelHistoryDto>();

        // Member Warning Mapping
        CreateMap<MemberWarning, MemberWarningDto>();

        // Notifications Mapping
        CreateMap<AppNotification, AppNotificationDto>();

        // Member Award Mapping
        CreateMap<MemberAward, MemberAwardDto>()
            .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src =>
                $"/api/awards/images/{(int)src.Level}?colored={src.Status == ProjectK.Common.Models.Enums.BadgeProgressStatus.Confirmed}"));

        // Leadership History Mapping
        CreateMap<LeadershipHistory, LeadershipHistoryDto>()
            .ForMember(dest => dest.LeadershipType, opt => opt.MapFrom(src => src.Leadership.Type))
            .ForMember(dest => dest.GroupName, opt => opt.MapFrom(src => src.Leadership.Group != null ? src.Leadership.Group.Name : null));

        CreateMap<LeadershipHistory, LeadershipHistoryMemberDto>()
            .ForMember(dest => dest.LeadershipKey, opt => opt.Ignore());

        CreateMap<LeadershipHistoryMemberDto, LeadershipHistory>()
            .EqualityComparison((src, dest) => src.LeadershipHistoryKey == dest.LeadershipHistoryKey)
            .ForMember(dest => dest.LeadershipHistoryKey, opt => opt.Ignore())
            .ForMember(dest => dest.Leadership, opt => opt.Ignore())
            .ForMember(dest => dest.MemberKey, opt => opt.MapFrom(src => src.Member.MemberKey))
            .ForMember(dest => dest.Member, opt => opt.Ignore())
            .ForMember(dest => dest.LeadershipKey, opt => opt.Ignore())
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role))
            .ForMember(dest => dest.StartDate, opt => opt.MapFrom(src => src.StartDate))
            .ForMember(dest => dest.EndDate, opt => opt.MapFrom(src => src.EndDate));

        // Leadership Mapping
        CreateMap<Leadership, LeadershipResponse>()
            .ForMember(dest => dest.EntityKey, opt => opt.MapFrom(src =>
                src.Type == LeadershipType.Group
                    ? (src.GroupKey ?? Guid.Empty)
                    : (src.KurinKey ?? Guid.Empty)));

        CreateMap<UpsertLeadershipCommand, Leadership>(MemberList.None)
            .ForMember(dest => dest.LeadershipKey, opt => opt.Ignore())
            .ForMember(dest => dest.Type, opt => opt.Ignore())
            .ForMember(dest => dest.GroupKey, opt => opt.Ignore())
            .ForMember(dest => dest.LeadershipHistories, opt => opt.MapFrom(src => src.LeadershipHistoryMembers));

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
