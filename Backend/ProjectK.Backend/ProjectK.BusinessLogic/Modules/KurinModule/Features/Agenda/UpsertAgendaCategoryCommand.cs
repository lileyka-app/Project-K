using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda;

/// <summary>Create (empty key) or edit an event group. Authorization is enforced at the controller (Зв'язковий).</summary>
public sealed record UpsertAgendaCategoryCommand : IRequest<ServiceResult<AgendaCategoryDto>>
{
    public Guid? AgendaCategoryKey { get; init; }
    public Guid KurinKey { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ColorHex { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public int? Capacity { get; init; }
    public bool WaitlistEnabled { get; init; }
    public string? DefaultDescription { get; init; }
    public bool RsvpRequired { get; init; }
    public int? DefaultDurationMinutes { get; init; }
    public int? ReminderLeadMinutes { get; init; }
    public bool IsArchived { get; init; }
    public bool IsKurinSchedule { get; init; }
}

public sealed class UpsertAgendaCategoryCommandHandler : IRequestHandler<UpsertAgendaCategoryCommand, ServiceResult<AgendaCategoryDto>>
{
    private readonly IUnitOfWork _uow;

    public UpsertAgendaCategoryCommandHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ServiceResult<AgendaCategoryDto>> Handle(UpsertAgendaCategoryCommand request, CancellationToken cancellationToken)
    {
        AgendaCategory category;
        var isNew = request.AgendaCategoryKey is null || request.AgendaCategoryKey == Guid.Empty;
        if (isNew)
        {
            category = new AgendaCategory { KurinKey = request.KurinKey };
            _uow.AgendaCategories.Create(category, cancellationToken);
        }
        else
        {
            var existing = await _uow.AgendaCategories.GetByKeyAsync(request.AgendaCategoryKey!.Value, cancellationToken);
            if (existing is null)
            {
                return ServiceResult<AgendaCategoryDto>.Failure(ResultType.NotFound, AgendaErrors.CategoryNotFound, "Event group was not found.");
            }

            if (existing.KurinKey != request.KurinKey)
            {
                return ServiceResult<AgendaCategoryDto>.Failure(ResultType.Forbidden, AgendaErrors.CategoryOtherKurin, "Event group belongs to a different kurin.");
            }

            category = existing;
            _uow.AgendaCategories.Update(category, cancellationToken);
        }

        category.Name = request.Name.Trim();
        category.ColorHex = request.ColorHex.Trim();
        category.Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim();
        category.Capacity = request.Capacity;
        category.WaitlistEnabled = request.WaitlistEnabled;
        category.DefaultDescription = string.IsNullOrWhiteSpace(request.DefaultDescription) ? null : request.DefaultDescription.Trim();
        category.RsvpRequired = request.RsvpRequired;
        category.DefaultDurationMinutes = request.DefaultDurationMinutes;
        category.ReminderLeadMinutes = request.ReminderLeadMinutes;
        category.IsArchived = request.IsArchived;
        category.IsKurinSchedule = request.IsKurinSchedule;
        category.UpdatedDate = DateTime.UtcNow;

        await _uow.SaveChangesAsync(cancellationToken);

        return new ServiceResult<AgendaCategoryDto>(
            isNew ? ResultType.Created : ResultType.Success,
            category.ToDto());
    }
}

public sealed class UpsertAgendaCategoryCommandValidator : AbstractValidator<UpsertAgendaCategoryCommand>
{
    public UpsertAgendaCategoryCommandValidator()
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.ColorHex).NotEmpty().MaximumLength(32);
        RuleFor(c => c.Icon).MaximumLength(64);
        RuleFor(c => c.DefaultDescription).MaximumLength(2000);
        RuleFor(c => c.Capacity).GreaterThan(0).When(c => c.Capacity.HasValue);
        RuleFor(c => c.DefaultDurationMinutes).GreaterThan(0).When(c => c.DefaultDurationMinutes.HasValue);
        RuleFor(c => c.ReminderLeadMinutes).GreaterThanOrEqualTo(0).When(c => c.ReminderLeadMinutes.HasValue);
    }
}
