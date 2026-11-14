using MediatR;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KraiModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;

/// <summary>Every saved revision of the край's profile, newest first.</summary>
public sealed record GetKraiProfileRevisionsQuery(string KraiCode) : IRequest<ServiceResult<IReadOnlyList<KraiProfileRevisionDto>>>;

public sealed class GetKraiProfileRevisionsQueryHandler : IRequestHandler<GetKraiProfileRevisionsQuery, ServiceResult<IReadOnlyList<KraiProfileRevisionDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetKraiProfileRevisionsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<IReadOnlyList<KraiProfileRevisionDto>>> Handle(GetKraiProfileRevisionsQuery request, CancellationToken cancellationToken)
    {
        var code = Krai.NormalizeCode(request.KraiCode) ?? string.Empty;
        if (!await _unitOfWork.Krais.ExistsAsync(code, cancellationToken))
        {
            return KraiErrors.NotFound<IReadOnlyList<KraiProfileRevisionDto>>();
        }

        var revisions = await _unitOfWork.Krais.GetProfileRevisionsAsync(code, cancellationToken);
        var authorKeys = revisions.Select(r => r.CreatedByUserKey).OfType<Guid>().ToHashSet();
        var names = authorKeys.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _unitOfWork.Users.GetAllAsync(cancellationToken))
                .Where(u => authorKeys.Contains(u.Id))
                .ToDictionary(u => u.Id, u => PersonNames.FirstLast(u.FirstName, u.LastName) is { Length: > 0 } name ? name : u.Email ?? string.Empty);

        IReadOnlyList<KraiProfileRevisionDto> response = revisions
            .Select(r => r.ToDto(r.CreatedByUserKey is { } key ? names.GetValueOrDefault(key) : null))
            .ToList();

        return new ServiceResult<IReadOnlyList<KraiProfileRevisionDto>>(ResultType.Success, response);
    }
}
