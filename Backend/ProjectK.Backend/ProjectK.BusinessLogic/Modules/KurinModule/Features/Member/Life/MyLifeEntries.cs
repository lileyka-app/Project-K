using FluentValidation;
using MediatR;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Life;

/// <summary>The вишколи, табори and заходи the person wrote into their life story.</summary>
public sealed record GetMyLifeEntriesQuery : IRequest<ServiceResult<IReadOnlyList<MemberLifeEntryDto>>>;

/// <summary>Writes a line of the life story; a <paramref name="Key"/> changes an existing one.</summary>
public sealed record SaveMyLifeEntryCommand(Guid? Key, SaveLifeEntryRequest Entry) : IRequest<ServiceResult<MemberLifeEntryDto>>;

public sealed record DeleteMyLifeEntryCommand(Guid Key) : IRequest<ServiceResult<object>>;

/// <summary>The person behind the token, whose life story it is. Nobody else reads or writes it.</summary>
public sealed class LifeStoryOwner
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IMemberDirectory _members;

    public LifeStoryOwner(ICurrentUserContext currentUser, IMemberDirectory members)
    {
        _currentUser = currentUser;
        _members = members;
    }

    /// <summary>Null when no person stands behind the account — an administrator without a card.</summary>
    public async Task<Guid?> MemberKeyAsync(CancellationToken cancellationToken)
        => _currentUser.UserId is { } userKey && userKey != Guid.Empty
            ? (await _members.FindByAccountAsync(userKey, cancellationToken))?.MemberKey
            : null;
}

public static class LifeEntries
{
    /// <summary>How many lines one life story may hold, so a script cannot fill the table.</summary>
    public const int MaxPerPerson = 500;

    public static MemberLifeEntryDto ToDto(MemberLifeEntry entry) => new(
        entry.MemberLifeEntryKey, entry.Kind, entry.Course, entry.Title, entry.Number,
        entry.StartDate, entry.EndDate, entry.Place, entry.Commandant, entry.Role);

    /// <summary>Copies what the person wrote, trimmed, dropping what does not belong to the kind.</summary>
    public static void Apply(MemberLifeEntry entry, SaveLifeEntryRequest request)
    {
        var training = request.Kind == LifeEntryKind.Training;
        entry.Kind = request.Kind;
        entry.Course = training ? request.Course : null;
        entry.Title = training && request.Course != TrainingCourse.Other ? null : Clean(request.Title);
        entry.Number = training ? request.Number : null;
        entry.StartDate = request.StartDate;
        entry.EndDate = request.EndDate;
        entry.Place = Clean(request.Place);
        entry.Commandant = Clean(request.Commandant);
        entry.Role = Clean(request.Role);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class SaveMyLifeEntryCommandValidator : AbstractValidator<SaveMyLifeEntryCommand>
{
    public SaveMyLifeEntryCommandValidator()
    {
        RuleFor(c => c.Entry).NotNull();
        RuleFor(c => c.Entry.Kind).IsInEnum();
        RuleFor(c => c.Entry.Course).NotNull().IsInEnum().When(c => c.Entry.Kind == LifeEntryKind.Training);
        RuleFor(c => c.Entry.Title).NotEmpty()
            .When(c => c.Entry.Kind != LifeEntryKind.Training || c.Entry.Course == TrainingCourse.Other);
        RuleFor(c => c.Entry.Title).MaximumLength(MemberLifeEntry.TitleMaxLength);
        RuleFor(c => c.Entry.Number).InclusiveBetween(1, 9999).When(c => c.Entry.Number.HasValue);
        RuleFor(c => c.Entry.StartDate).NotEqual(default(DateOnly));
        RuleFor(c => c.Entry.EndDate).GreaterThanOrEqualTo(c => c.Entry.StartDate).When(c => c.Entry.EndDate.HasValue);
        RuleFor(c => c.Entry.Place).MaximumLength(MemberLifeEntry.PlaceMaxLength);
        RuleFor(c => c.Entry.Commandant).MaximumLength(MemberLifeEntry.PersonMaxLength);
        RuleFor(c => c.Entry.Role).MaximumLength(MemberLifeEntry.RoleMaxLength);
    }
}

public sealed class GetMyLifeEntriesQueryHandler : IRequestHandler<GetMyLifeEntriesQuery, ServiceResult<IReadOnlyList<MemberLifeEntryDto>>>
{
    private readonly LifeStoryOwner _owner;
    private readonly IMemberUnitOfWork _unitOfWork;

    public GetMyLifeEntriesQueryHandler(LifeStoryOwner owner, IMemberUnitOfWork unitOfWork)
    {
        _owner = owner;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<IReadOnlyList<MemberLifeEntryDto>>> Handle(GetMyLifeEntriesQuery request, CancellationToken cancellationToken)
    {
        if (await _owner.MemberKeyAsync(cancellationToken) is not { } memberKey)
        {
            return new ServiceResult<IReadOnlyList<MemberLifeEntryDto>>(ResultType.Forbidden);
        }

        var entries = await _unitOfWork.MemberLifeEntries.GetForMemberAsync(memberKey, cancellationToken);
        return new ServiceResult<IReadOnlyList<MemberLifeEntryDto>>(ResultType.Success, entries.Select(LifeEntries.ToDto).ToList());
    }
}

public sealed class SaveMyLifeEntryCommandHandler : IRequestHandler<SaveMyLifeEntryCommand, ServiceResult<MemberLifeEntryDto>>
{
    private readonly LifeStoryOwner _owner;
    private readonly IMemberUnitOfWork _unitOfWork;

    public SaveMyLifeEntryCommandHandler(LifeStoryOwner owner, IMemberUnitOfWork unitOfWork)
    {
        _owner = owner;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<MemberLifeEntryDto>> Handle(SaveMyLifeEntryCommand request, CancellationToken cancellationToken)
    {
        if (await _owner.MemberKeyAsync(cancellationToken) is not { } memberKey)
        {
            return new ServiceResult<MemberLifeEntryDto>(ResultType.Forbidden);
        }

        MemberLifeEntry entry;
        if (request.Key is { } key)
        {
            // Someone else's entry answers as missing: whether it exists is not the caller's to learn.
            if (await _unitOfWork.MemberLifeEntries.GetOwnAsync(memberKey, key, cancellationToken) is not { } own)
            {
                return new ServiceResult<MemberLifeEntryDto>(ResultType.NotFound);
            }

            entry = own;
            LifeEntries.Apply(entry, request.Entry);
            entry.UpdatedDate = DateTime.UtcNow;
            _unitOfWork.MemberLifeEntries.Update(entry, cancellationToken);
        }
        else
        {
            var count = (await _unitOfWork.MemberLifeEntries.GetForMemberAsync(memberKey, cancellationToken)).Count;
            if (count >= LifeEntries.MaxPerPerson)
            {
                return ServiceResult<MemberLifeEntryDto>.Failure(ResultType.BadRequest, "LifeStoryFull", "The life story holds no more entries.");
            }

            entry = new MemberLifeEntry { MemberKey = memberKey };
            LifeEntries.Apply(entry, request.Entry);
            _unitOfWork.MemberLifeEntries.Create(entry, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<MemberLifeEntryDto>(ResultType.Success, LifeEntries.ToDto(entry));
    }
}

public sealed class DeleteMyLifeEntryCommandHandler : IRequestHandler<DeleteMyLifeEntryCommand, ServiceResult<object>>
{
    private readonly LifeStoryOwner _owner;
    private readonly IMemberUnitOfWork _unitOfWork;

    public DeleteMyLifeEntryCommandHandler(LifeStoryOwner owner, IMemberUnitOfWork unitOfWork)
    {
        _owner = owner;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<object>> Handle(DeleteMyLifeEntryCommand request, CancellationToken cancellationToken)
    {
        if (await _owner.MemberKeyAsync(cancellationToken) is not { } memberKey)
        {
            return new ServiceResult<object>(ResultType.Forbidden);
        }

        if (await _unitOfWork.MemberLifeEntries.GetOwnAsync(memberKey, request.Key, cancellationToken) is not { } entry)
        {
            return new ServiceResult<object>(ResultType.NotFound);
        }

        _unitOfWork.MemberLifeEntries.Delete(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<object>(ResultType.Success);
    }
}
