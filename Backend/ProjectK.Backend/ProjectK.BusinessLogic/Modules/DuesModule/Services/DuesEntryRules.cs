using FluentValidation;
using ProjectK.Common.Models.Dtos.DuesModule.Requests;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>
/// What any operation has to satisfy before it reaches a box. Shared by create and update, and by
/// the гурток's box and the kurin's, so none of them can drift apart.
/// </summary>
public static class DuesEntryRules
{
    /// <summary>Kinds that are about one person's вкладка and so need a membership.</summary>
    public static readonly IReadOnlySet<DuesEntryKind> PersonalKinds =
        new HashSet<DuesEntryKind> { DuesEntryKind.Contribution, DuesEntryKind.Refund, DuesEntryKind.Correction };

    /// <summary>Kinds a гурток's box may hold. Handing to the станиця or the край is the kurin's business.</summary>
    public static readonly IReadOnlySet<DuesEntryKind> GroupKinds = new HashSet<DuesEntryKind>
    {
        DuesEntryKind.Contribution, DuesEntryKind.Refund, DuesEntryKind.Correction,
        DuesEntryKind.TransferToKurin, DuesEntryKind.Expense, DuesEntryKind.OtherIncome, DuesEntryKind.Exchange
    };

    /// <summary>
    /// Kinds the kurin's own box may hold. Nothing personal: people pay their гурток, and what
    /// reaches the kurin arrives as a гурток's transfer, not as an operation written here.
    /// </summary>
    public static readonly IReadOnlySet<DuesEntryKind> KurinKinds = new HashSet<DuesEntryKind>
    {
        DuesEntryKind.Expense, DuesEntryKind.OtherIncome, DuesEntryKind.Exchange, DuesEntryKind.TransferToStanytsia,
        DuesEntryKind.TransferToKrai
    };

    public static void Apply(AbstractValidator<UpsertDuesEntryRequest> validator, TimeProvider time, IReadOnlySet<DuesEntryKind> allowedKinds)
    {
        validator.RuleFor(r => r.Kind).IsInEnum().Must(allowedKinds.Contains)
            .WithMessage("This box does not hold that kind of operation.");
        validator.RuleFor(r => r.Method).IsInEnum();
        validator.RuleFor(r => r.Amount).NotEqual(0).WithMessage("An amount is required.");
        validator.RuleFor(r => r.Amount).GreaterThan(0)
            .When(r => r.Kind != DuesEntryKind.Correction)
            .WithMessage("Only a correction may lower a balance.");
        validator.RuleFor(r => r.Amount).PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .WithMessage("Money is kept to the kopiyka.");
        validator.RuleFor(r => r.OccurredOn).NotEmpty();
        validator.RuleFor(r => r.OccurredOn)
            .Must(date => date <= DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).AddDays(1))
            .WithMessage("An operation cannot be dated in the future.");
        validator.RuleFor(r => r.MembershipKey).NotEmpty()
            .When(r => PersonalKinds.Contains(r.Kind))
            .WithMessage("Say whose вкладка this is.");
        validator.RuleFor(r => r.MembershipKey).Null()
            .When(r => !PersonalKinds.Contains(r.Kind))
            .WithMessage("That kind of operation is not about one person.");
        validator.RuleFor(r => r.CounterMethod).NotNull().NotEqual(r => r.Method)
            .When(r => r.Kind == DuesEntryKind.Exchange)
            .WithMessage("An exchange needs the other side: cash to card or card to cash.");
        validator.RuleFor(r => r.CounterMethod).Null()
            .When(r => r.Kind != DuesEntryKind.Exchange);
        validator.RuleFor(r => r.Note).MaximumLength(500);
    }
}

/// <summary>An operation as a гурток's box accepts it.</summary>
public sealed class UpsertDuesEntryRequestValidator : AbstractValidator<UpsertDuesEntryRequest>
{
    public UpsertDuesEntryRequestValidator(TimeProvider time)
    {
        DuesEntryRules.Apply(this, time, DuesEntryRules.GroupKinds);
    }
}

/// <summary>An operation as the kurin's own box accepts it.</summary>
public sealed class UpsertKurinDuesEntryRequestValidator : AbstractValidator<UpsertDuesEntryRequest>
{
    public UpsertKurinDuesEntryRequestValidator(TimeProvider time)
    {
        DuesEntryRules.Apply(this, time, DuesEntryRules.KurinKinds);
    }
}
