using MediatR;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Membership;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Services;

/// <inheritdoc />
public sealed class MemberImportWriter : IMemberImportWriter
{
    private readonly ISender _sender;

    public MemberImportWriter(ISender sender)
    {
        _sender = sender;
    }

    public async Task<Guid?> CreateAsync(ImportedMember person, CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new UpsertMemberProfileCommand
        {
            MemberKey = Guid.Empty,
            KurinKey = person.KurinKey,
            GroupKey = person.GroupKey,
            FirstName = person.FirstName,
            MiddleName = person.MiddleName,
            LastName = person.LastName,
            Email = person.Email,
            PhoneNumber = person.PhoneNumber,
            DateOfBirth = person.DateOfBirth,
            Address = person.Address,
            School = person.School,
            PlastLevelHistories = person.PlastLevelHistories.ToList()
        }, cancellationToken);

        return result.Type == ResultType.Success && result.Data is not null
            ? result.Data.MemberKey
            : null;
    }

    public async Task<bool> JoinKurinAsync(Guid memberKey, Guid kurinKey, Guid? groupKey, CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new JoinKurinCommand(memberKey, kurinKey, groupKey), cancellationToken);

        // Conflict means they were already taken in. Nothing went wrong and nothing needs undoing.
        return result.Type is ResultType.Success or ResultType.Created or ResultType.Conflict;
    }

    public Task<ServiceResult<MemberInvitation>> InviteAsync(Guid memberKey, CancellationToken cancellationToken = default)
        => _sender.Send(new ProvisionMemberAccountCommand(memberKey), cancellationToken);
}
