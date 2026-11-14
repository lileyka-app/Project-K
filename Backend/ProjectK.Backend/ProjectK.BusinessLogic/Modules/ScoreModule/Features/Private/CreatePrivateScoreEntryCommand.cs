using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Models.Dtos.ScoreModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Private;

public sealed record CreatePrivateScoreEntryCommand(Guid KurinKey, UpsertPrivateScoreEntryRequest Request) : IRequest<ServiceResult<Guid>>;

public sealed class CreatePrivateScoreEntryCommandValidator : AbstractValidator<CreatePrivateScoreEntryCommand>
{
    public CreatePrivateScoreEntryCommandValidator(TimeProvider time)
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().SetValidator(new UpsertPrivateScoreEntryRequestValidator(time));
    }
}

public sealed class UpsertPrivateScoreEntryRequestValidator : AbstractValidator<UpsertPrivateScoreEntryRequest>
{
    public UpsertPrivateScoreEntryRequestValidator(TimeProvider time)
    {
        RuleFor(r => r.MembershipKey).NotEmpty();
        RuleFor(r => r.Points).NotEqual(0).InclusiveBetween(-1000, 1000);
        RuleFor(r => r.Note).MaximumLength(500);
        RuleFor(r => r).Must(r => r.PrivateScoreCriterionKey.HasValue || !string.IsNullOrWhiteSpace(r.Note))
            .WithMessage("Say what it was for: a criterion, or a note.");
        RuleFor(r => r.OccurredOn).LessThanOrEqualTo(_ => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).AddDays(1));
    }
}

public sealed class CreatePrivateScoreEntryCommandHandler : IRequestHandler<CreatePrivateScoreEntryCommand, ServiceResult<Guid>>
{
    private readonly PrivateScoreAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IScoreUnitOfWork _score;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public CreatePrivateScoreEntryCommandHandler(PrivateScoreAccess access, ScoreBookReader books, IScoreUnitOfWork score, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _books = books;
        _score = score;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Guid>> Handle(CreatePrivateScoreEntryCommand request, CancellationToken cancellationToken)
    {
        if (await _access.RefuseAsync<Guid>(request.KurinKey, ResourceAction.Create, cancellationToken) is { } refused)
        {
            return refused;
        }

        var book = await _books.OpenAsync(request.KurinKey, cancellationToken);
        if (await PrivateScoreEntryWriter.CheckAsync<Guid>(_score, book, request.Request, cancellationToken) is { } problem)
        {
            return problem;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var entry = new PrivateScoreEntry { KurinKey = request.KurinKey, MembershipKey = request.Request.MembershipKey, CreatedByUserKey = _currentUser.UserId, CreatedDate = now, UpdatedDate = now };
        PrivateScoreEntryWriter.Apply(entry, request.Request);
        _score.PrivateScoreEntries.Create(entry, cancellationToken);
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.PrivateEntry, entry.PrivateScoreEntryKey, ScoreTrail.Created, PrivateScoreEntryWriter.Snapshot(entry), _currentUser.UserId, now);

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Guid>(ResultType.Created, entry.PrivateScoreEntryKey);
    }
}
