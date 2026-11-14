using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Models.Dtos.ScoreModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Entry;

public sealed record CreateScoreEntryCommand(Guid KurinKey, UpsertScoreEntryRequest Request) : IRequest<ServiceResult<Guid>>;

public sealed class CreateScoreEntryCommandValidator : AbstractValidator<CreateScoreEntryCommand>
{
    public CreateScoreEntryCommandValidator(TimeProvider time)
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().SetValidator(new UpsertScoreEntryRequestValidator(time));
    }
}

/// <summary>
/// The shape of points given by hand: one target, a position or a free amount with a reason, and an
/// occurrence whenever an event is named. The day may not be in the future.
/// </summary>
public sealed class UpsertScoreEntryRequestValidator : AbstractValidator<UpsertScoreEntryRequest>
{
    public UpsertScoreEntryRequestValidator(TimeProvider time)
    {
        RuleFor(r => r).Must(r => r.MembershipKey.HasValue != r.GroupKey.HasValue)
            .WithMessage("Points go to a person or to a гурток — one of the two.");
        RuleFor(r => r).Must(r => r.ScoreItemKey.HasValue || (r.Points.HasValue && !string.IsNullOrWhiteSpace(r.Reason)))
            .WithMessage("Pick a position from the list, or give an amount and a reason.");
        RuleFor(r => r.Points).NotEqual(0).When(r => r.Points.HasValue).WithMessage("Zero points change nothing.");
        RuleFor(r => r.Points).InclusiveBetween(-1000, 1000).When(r => r.Points.HasValue);
        RuleFor(r => r.Reason).MaximumLength(500);
        RuleFor(r => r.OccurrenceStartUtc).NotNull().When(r => r.AgendaItemKey.HasValue)
            .WithMessage("An event is named by its occurrence too.");
        RuleFor(r => r.OccurredOn).LessThanOrEqualTo(_ => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).AddDays(1))
            .WithMessage("Points are not given ahead of the day.");
    }
}

public sealed class CreateScoreEntryCommandHandler : IRequestHandler<CreateScoreEntryCommand, ServiceResult<Guid>>
{
    private readonly ScoreAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IScoreUnitOfWork _score;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;
    private readonly IKraiProfileProvider _profiles;

    public CreateScoreEntryCommandHandler(ScoreAccess access, ScoreBookReader books, IScoreUnitOfWork score, IUnitOfWork unitOfWork, ICurrentUserContext currentUser, TimeProvider time, IKraiProfileProvider profiles)
    {
        _access = access;
        _books = books;
        _score = score;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _time = time;
        _profiles = profiles;
    }

    public async Task<ServiceResult<Guid>> Handle(CreateScoreEntryCommand request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<Guid>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var book = await _books.OpenAsync(request.KurinKey, cancellationToken);
        if (await ScoreEntryWriter.CheckAsync<Guid>(_access, _unitOfWork, _profiles, book, request.Request, null, cancellationToken) is { } problem)
        {
            return problem;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var entry = new ScoreEntry
        {
            KurinKey = request.KurinKey,
            MembershipKey = request.Request.MembershipKey,
            GroupKey = request.Request.GroupKey,
            CreatedByUserKey = _currentUser.UserId,
            CreatedDate = now,
            UpdatedDate = now
        };
        ScoreEntryWriter.Apply(entry, request.Request, book);
        _score.ScoreEntries.Create(entry, cancellationToken);
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.Entry, entry.ScoreEntryKey, ScoreTrail.Created, ScoreEntryWriter.Snapshot(entry), _currentUser.UserId, now);

        try
        {
            await _score.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateRowException)
        {
            return ScoreEntryWriter.GivenMeanwhile<Guid>();
        }

        return new ServiceResult<Guid>(ResultType.Created, entry.ScoreEntryKey);
    }
}
