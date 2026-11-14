using FluentValidation;
using MediatR;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Dtos.MemberModule.Requests;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Life;

/// <summary>Writes a line of the life story; a <paramref name="Key"/> changes an existing one.</summary>
public sealed record SaveMyLifeEntryCommand(Guid? Key, SaveLifeEntryRequest Entry) : IRequest<ServiceResult<MemberLifeEntryDto>>;

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

public sealed class SaveMyLifeEntryCommandHandler : IRequestHandler<SaveMyLifeEntryCommand, ServiceResult<MemberLifeEntryDto>>
{
    private readonly ICurrentMember _owner;
    private readonly IMemberUnitOfWork _unitOfWork;

    public SaveMyLifeEntryCommandHandler(ICurrentMember owner, IMemberUnitOfWork unitOfWork)
    {
        _owner = owner;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<MemberLifeEntryDto>> Handle(SaveMyLifeEntryCommand request, CancellationToken cancellationToken)
    {
        if (await _owner.MemberKeyAsync(cancellationToken) is not { } memberKey)
        {
            return ServiceResult<MemberLifeEntryDto>.Failure(ResultType.Forbidden, MeErrors.NoOwnCard, "No person stands behind this account.");
        }

        MemberLifeEntry entry;
        if (request.Key is { } key)
        {
            // Someone else's entry answers as missing: whether it exists is not the caller's to learn.
            if (await _unitOfWork.MemberLifeEntries.GetOwnAsync(memberKey, key, cancellationToken) is not { } own)
            {
                return ServiceResult<MemberLifeEntryDto>.Failure(ResultType.NotFound, MeErrors.LifeEntryNotFound, "No such entry in your life story.");
            }

            entry = own;
            LifeMapping.Apply(entry, request.Entry);
            entry.UpdatedDate = DateTime.UtcNow;
            _unitOfWork.MemberLifeEntries.Update(entry, cancellationToken);
        }
        else
        {
            var count = (await _unitOfWork.MemberLifeEntries.GetForMemberAsync(memberKey, cancellationToken)).Count;
            if (count >= LifeMapping.MaxPerPerson)
            {
                return ServiceResult<MemberLifeEntryDto>.Failure(ResultType.BadRequest, MeErrors.LifeStoryFull, "The life story holds no more entries.");
            }

            entry = new MemberLifeEntry { MemberKey = memberKey };
            LifeMapping.Apply(entry, request.Entry);
            _unitOfWork.MemberLifeEntries.Create(entry, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<MemberLifeEntryDto>(ResultType.Success, LifeMapping.ToDto(entry));
    }
}
