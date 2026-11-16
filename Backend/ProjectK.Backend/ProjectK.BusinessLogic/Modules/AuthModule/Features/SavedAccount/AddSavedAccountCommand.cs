using MediatR;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuthModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.SavedAccount;

/// <summary>
/// A sign-in that adds an account rather than replacing one: the session the browser was using is
/// kept alongside the ones it already kept, so the person can switch back to it.
/// </summary>
/// <param name="SavedTokens">The sessions the browser already kept.</param>
/// <param name="PreviousTokens">The session the browser was using before this sign-in.</param>
/// <param name="SignedInUserKey">The account that just signed in; any session it already had here is ended.</param>
public sealed record AddSavedAccountCommand(
    IReadOnlyList<string> SavedTokens,
    IReadOnlyList<string> PreviousTokens,
    Guid SignedInUserKey) : IRequest<ServiceResult<IReadOnlyList<SavedSession>>>;

public sealed class AddSavedAccountCommandHandler : IRequestHandler<AddSavedAccountCommand, ServiceResult<IReadOnlyList<SavedSession>>>
{
    private readonly ISavedSessionList _saved;

    public AddSavedAccountCommandHandler(ISavedSessionList saved)
    {
        _saved = saved;
    }

    public async Task<ServiceResult<IReadOnlyList<SavedSession>>> Handle(AddSavedAccountCommand request, CancellationToken cancellationToken)
    {
        var saved = await _saved.TidyAsync(
            request.SavedTokens.Concat(request.PreviousTokens),
            request.SignedInUserKey,
            cancellationToken);

        return new ServiceResult<IReadOnlyList<SavedSession>>(ResultType.Success, saved);
    }
}
