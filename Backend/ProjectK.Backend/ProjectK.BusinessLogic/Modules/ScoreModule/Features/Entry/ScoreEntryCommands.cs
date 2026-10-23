using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Exceptions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Common.Models.Dtos.ScoreModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Entry;

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

/// <summary>The checks a new and an edited entry share, with the book open.</summary>
public static class ScoreEntryWriter
{
    /// <summary>Null when the entry may be written as asked; otherwise why not.</summary>
    public static async Task<ServiceResult<T>?> CheckAsync<T>(
        ScoreAccess access,
        IUnitOfWork unitOfWork,
        IKraiProfileProvider profiles,
        ScoreBook book,
        UpsertScoreEntryRequest request,
        Guid? editingKey,
        CancellationToken cancellationToken)
    {
        if (ResolveGroup<T>(book, request, out var groupKey) is { } unknownTarget)
        {
            return unknownTarget;
        }

        if (!await access.MayScoreAsync(book.KurinKey, groupKey, ResourceAction.Create, cancellationToken))
        {
            return ScoreAccess.Forbidden<T>();
        }

        if (request.ScoreItemKey is { } itemKey && book.Items.All(i => i.ScoreItemKey != itemKey || i.IsArchived))
        {
            return ScoreAccess.NotFound<T>("There is no such position on the list.");
        }

        if (request.AgendaItemKey is { } eventKey)
        {
            var occurrence = await EventOccurrence.LoadAsync(unitOfWork, profiles, book.KurinKey, eventKey, request.OccurrenceStartUtc!.Value, cancellationToken);
            if (occurrence is null)
            {
                return ScoreAccess.NotFound<T>("There is no such event, or it does not meet that day.");
            }

            if (request.ScoreItemKey is { } positionKey)
            {
                return AlreadyGiven<T>(book, request, editingKey, positionKey, eventKey, occurrence.StartUtc);
            }
        }

        return null;
    }

    private static ServiceResult<T>? ResolveGroup<T>(ScoreBook book, UpsertScoreEntryRequest request, out Guid? groupKey)
    {
        groupKey = null;
        if (request.MembershipKey is { } membershipKey)
        {
            if (!book.Memberships.TryGetValue(membershipKey, out var membership) || membership.Kind != MembershipKind.Youth)
            {
                return ScoreAccess.NotFound<T>("That person is not a youth of this kurin.");
            }

            groupKey = membership.LeftAtUtc is null ? membership.GroupKey : null;
        }
        else
        {
            groupKey = request.GroupKey;
            if (!book.GroupNames.ContainsKey(groupKey!.Value))
            {
                return ScoreAccess.NotFound<T>("There is no such гурток here.");
            }
        }

        return null;
    }

    private static ServiceResult<T>? AlreadyGiven<T>(ScoreBook book, UpsertScoreEntryRequest request, Guid? editingKey, Guid positionKey, Guid eventKey, DateTime occurrenceStartUtc)
    {
        var already = book.Entries.FirstOrDefault(e =>
            e.ScoreEntryKey != editingKey
            && e.ScoreItemKey == positionKey
            && e.AgendaItemKey == eventKey
            && e.OccurrenceStartUtc == occurrenceStartUtc
            && e.MembershipKey == request.MembershipKey
            && e.GroupKey == request.GroupKey);
        if (already is not null)
        {
            return ServiceResult<T>.Failure(ResultType.Conflict, "ItemAlreadyGiven",
                $"Уже дав {book.NameOfAccount(already.CreatedByUserKey) ?? "хтось"}.");
        }

        return null;
    }

    /// <summary>
    /// The same position was given for the same event between our check and our save — the unique
    /// index caught what the check could not. Said the way the check would have said it.
    /// </summary>
    public static ServiceResult<T> GivenMeanwhile<T>() =>
        ServiceResult<T>.Failure(ResultType.Conflict, "ItemAlreadyGiven", "Уже дав хтось інший щойно.");

    public static void Apply(ScoreEntry entry, UpsertScoreEntryRequest request, ScoreBook book)
    {
        entry.ScoreItemKey = request.ScoreItemKey;
        entry.Points = request.ScoreItemKey is { } itemKey
            ? book.Items.First(i => i.ScoreItemKey == itemKey).Points
            : request.Points!.Value;
        entry.Reason = request.ScoreItemKey.HasValue ? null : request.Reason!.Trim();
        entry.AgendaItemKey = request.AgendaItemKey;
        entry.OccurrenceStartUtc = request.AgendaItemKey.HasValue ? DateTime.SpecifyKind(request.OccurrenceStartUtc!.Value, DateTimeKind.Utc) : null;
        entry.OccurredOn = request.OccurredOn;
    }

    public static object Snapshot(ScoreEntry e) => new
    {
        e.MembershipKey,
        e.GroupKey,
        e.ScoreItemKey,
        e.Points,
        e.Reason,
        e.AgendaItemKey,
        e.OccurrenceStartUtc,
        e.OccurredOn,
        e.DeletedAtUtc,
        e.DeletedByUserKey
    };
}

public sealed record CreateScoreEntryCommand(Guid KurinKey, UpsertScoreEntryRequest Request) : IRequest<ServiceResult<Guid>>;

public sealed class CreateScoreEntryCommandValidator : AbstractValidator<CreateScoreEntryCommand>
{
    public CreateScoreEntryCommandValidator(TimeProvider time)
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().SetValidator(new UpsertScoreEntryRequestValidator(time));
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

/// <summary>Changes what was given, not to whom: the target of an entry is what it is.</summary>
public sealed record UpdateScoreEntryCommand(Guid KurinKey, Guid EntryKey, UpsertScoreEntryRequest Request) : IRequest<ServiceResult<object>>;

public sealed class UpdateScoreEntryCommandValidator : AbstractValidator<UpdateScoreEntryCommand>
{
    public UpdateScoreEntryCommandValidator(TimeProvider time)
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.EntryKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().SetValidator(new UpsertScoreEntryRequestValidator(time));
    }
}

public sealed class UpdateScoreEntryCommandHandler : IRequestHandler<UpdateScoreEntryCommand, ServiceResult<object>>
{
    private readonly ScoreAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IScoreUnitOfWork _score;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;
    private readonly IKraiProfileProvider _profiles;

    public UpdateScoreEntryCommandHandler(ScoreAccess access, ScoreBookReader books, IScoreUnitOfWork score, IUnitOfWork unitOfWork, ICurrentUserContext currentUser, TimeProvider time, IKraiProfileProvider profiles)
    {
        _access = access;
        _books = books;
        _score = score;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _time = time;
        _profiles = profiles;
    }

    public async Task<ServiceResult<object>> Handle(UpdateScoreEntryCommand request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<object>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var entry = await _score.ScoreEntries.GetByKeyAsync(request.EntryKey, cancellationToken);
        if (entry is null || entry.KurinKey != request.KurinKey || entry.IsDeleted)
        {
            return ScoreAccess.NotFound<object>("There is no such entry here.");
        }

        if (entry.MembershipKey != request.Request.MembershipKey || entry.GroupKey != request.Request.GroupKey)
        {
            return ServiceResult<object>.Failure(ResultType.BadRequest, "EntryTargetFixed", "An entry stays with whom it was given to; give a new one instead.");
        }

        var book = await _books.OpenAsync(request.KurinKey, cancellationToken);
        if (await ScoreEntryWriter.CheckAsync<object>(_access, _unitOfWork, _profiles, book, request.Request, entry.ScoreEntryKey, cancellationToken) is { } problem)
        {
            return problem;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        ScoreEntryWriter.Apply(entry, request.Request, book);
        entry.UpdatedDate = now;
        _score.ScoreEntries.Update(entry, cancellationToken);
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.Entry, entry.ScoreEntryKey, ScoreTrail.Updated, ScoreEntryWriter.Snapshot(entry), _currentUser.UserId, now);

        try
        {
            await _score.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateRowException)
        {
            return ScoreEntryWriter.GivenMeanwhile<object>();
        }

        return new ServiceResult<object>(ResultType.Success);
    }
}

public sealed record DeleteScoreEntryCommand(Guid KurinKey, Guid EntryKey) : IRequest<ServiceResult<object>>;

public sealed class DeleteScoreEntryCommandHandler : IRequestHandler<DeleteScoreEntryCommand, ServiceResult<object>>
{
    private readonly ScoreAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IScoreUnitOfWork _score;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public DeleteScoreEntryCommandHandler(ScoreAccess access, ScoreBookReader books, IScoreUnitOfWork score, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _books = books;
        _score = score;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<object>> Handle(DeleteScoreEntryCommand request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<object>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var entry = await _score.ScoreEntries.GetByKeyAsync(request.EntryKey, cancellationToken);
        if (entry is null || entry.KurinKey != request.KurinKey || entry.IsDeleted)
        {
            return ScoreAccess.NotFound<object>("There is no such entry here.");
        }

        var book = await _books.OpenAsync(request.KurinKey, cancellationToken);
        var groupKey = entry.GroupKey ?? (book.Memberships.TryGetValue(entry.MembershipKey!.Value, out var m) && m.LeftAtUtc is null ? m.GroupKey : null);
        if (!await _access.MayScoreAsync(request.KurinKey, groupKey, ResourceAction.Delete, cancellationToken))
        {
            return ScoreAccess.Forbidden<object>();
        }

        var now = _time.GetUtcNow().UtcDateTime;
        entry.DeletedAtUtc = now;
        entry.DeletedByUserKey = _currentUser.UserId;
        entry.UpdatedDate = now;
        _score.ScoreEntries.Update(entry, cancellationToken);
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.Entry, entry.ScoreEntryKey, ScoreTrail.Deleted, ScoreEntryWriter.Snapshot(entry), _currentUser.UserId, now);

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<object>(ResultType.Success);
    }
}
