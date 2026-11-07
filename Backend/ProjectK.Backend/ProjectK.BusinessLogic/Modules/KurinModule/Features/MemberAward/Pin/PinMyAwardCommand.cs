using FluentValidation;
using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Pin;

/// <summary>
/// Pins one of the caller's own відзначення to the front of their card, or unpins it: every grant of
/// that відзначення at once — an УПЮ one by <see cref="Level"/>, a senior one by <see cref="CatalogId"/>.
/// Only one's own: what the person likes best is theirs to say.
/// </summary>
public sealed class PinMyAwardCommand : IRequest<ServiceResult<bool>>
{
    public MemberAwardLevel? Level { get; set; }
    public string? CatalogId { get; set; }
    public bool Pinned { get; set; }
}

public sealed class PinMyAwardCommandValidator : AbstractValidator<PinMyAwardCommand>
{
    public PinMyAwardCommandValidator()
    {
        RuleFor(command => command)
            .Must(command => (command.Level is null) != string.IsNullOrWhiteSpace(command.CatalogId))
            .WithName(nameof(PinMyAwardCommand.Level))
            .WithMessage("A відзначення is either an УПЮ level or a senior one from the catalogue.");
        RuleFor(command => command.Level).IsInEnum().When(command => command.Level is not null);
    }
}

public sealed class PinMyAwardCommandHandler : IRequestHandler<PinMyAwardCommand, ServiceResult<bool>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;

    public PinMyAwardCommandHandler(IMemberUnitOfWork unitOfWork, ICurrentUserContext currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<bool>> Handle(PinMyAwardCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userKey
            || await _unitOfWork.Members.GetSummaryByUserKeyAsync(userKey, cancellationToken) is not { } me)
        {
            return new ServiceResult<bool>(ResultType.Forbidden);
        }

        var catalogId = request.CatalogId?.Trim();
        var grants = (await _unitOfWork.MemberAwards.GetByMemberKeyAsync(me.MemberKey, cancellationToken))
            .Where(award => request.Level is { } level ? award.Level == level : award.CatalogId == catalogId)
            .ToList();
        if (grants.Count == 0)
        {
            return new ServiceResult<bool>(ResultType.NotFound);
        }

        foreach (var award in grants)
        {
            award.IsPinned = request.Pinned;
            _unitOfWork.MemberAwards.Update(award, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<bool>(ResultType.Success, request.Pinned);
    }
}
