using FluentValidation;
using MediatR;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Awards;

/// <summary>
/// Pins one of the caller's own відзначення to the front of their card, or unpins it: every grant of
/// that відзначення at once — an УПЮ one by <see cref="Level"/>, a senior one by <see cref="CatalogId"/>.
/// Only one's own: what the person likes best is theirs to say.
/// </summary>
public sealed class PinMyAwardCommand : IRequest<ServiceResult<Unit>>
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

public sealed class PinMyAwardCommandHandler : IRequestHandler<PinMyAwardCommand, ServiceResult<Unit>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly ICurrentMember _currentMember;

    public PinMyAwardCommandHandler(IMemberUnitOfWork unitOfWork, ICurrentMember currentMember)
    {
        _unitOfWork = unitOfWork;
        _currentMember = currentMember;
    }

    public async Task<ServiceResult<Unit>> Handle(PinMyAwardCommand request, CancellationToken cancellationToken)
    {
        if (await _currentMember.FindAsync(cancellationToken) is not { } me)
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, MeErrors.NoOwnCard, "No person stands behind this account.");
        }

        var catalogId = request.CatalogId?.Trim();
        var grants = (await _unitOfWork.MemberAwards.GetByMemberKeyAsync(me.MemberKey, cancellationToken))
            .Where(award => request.Level is { } level ? award.Level == level : award.CatalogId == catalogId)
            .ToList();
        if (grants.Count == 0)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, MeErrors.AwardNotFound, "You hold no such award.");
        }

        foreach (var award in grants)
        {
            award.IsPinned = request.Pinned;
            _unitOfWork.MemberAwards.Update(award, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
