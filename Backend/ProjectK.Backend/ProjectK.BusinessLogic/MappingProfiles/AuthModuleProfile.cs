using AutoMapper;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.Login;
using ProjectK.Common.Models.Dtos.AuthModule.Requests;

namespace ProjectK.BusinessLogic.MappingProfiles;

public class AuthModuleProfile : Profile
{
    public AuthModuleProfile()
    {
        CreateMap<LoginUserRequest, LoginUserCommand>()
            .ForMember(dest => dest.MfaTrustToken, opt => opt.Ignore());
    }
}
