using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.Dtos.DuesModule.Requests;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin.SetRate;

/// <summary>
/// Sets the kurin's статті of the вкладка from a quarter on, for every гурток of the kurin. A rate
/// already set from that quarter is replaced.
/// </summary>
public sealed record SetKurinDuesRateCommand(Guid KurinKey, SetKurinDuesRateRequest Request) : IRequest<ServiceResult<object>>;

public sealed class SetKurinDuesRateCommandValidator : AbstractValidator<SetKurinDuesRateCommand>
{
    public SetKurinDuesRateCommandValidator()
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Request.FromQuarter.Number).InclusiveBetween(1, 4);
        RuleFor(c => c.Request.FromQuarter.Year).InclusiveBetween(2000, 2100);

        When(c => c.Request.Articles is not null, () =>
        {
            RuleForEach(c => c.Request.Articles!).NotNull();
            RuleFor(c => c.Request.Articles!)
                .NotEmpty().WithMessage("A rate needs at least one стаття.")
                .Must(a => a.Count <= KurinDuesArticles.MaxCount)
                .WithMessage($"No more than {KurinDuesArticles.MaxCount} статей.")
                .Must(a => a.Select(x => x?.Name?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == a.Count)
                .WithMessage("Two статті cannot have the same name.");
            RuleForEach(c => c.Request.Articles!).ChildRules(article =>
            {
                article.RuleFor(a => a.Name).NotEmpty().Must(n => n?.Trim().Length > 0)
                    .MaximumLength(KurinDuesArticles.MaxNameLength);
                article.RuleFor(a => a.Recipient).IsInEnum();
                article.RuleFor(a => a.Amount).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
                article.RuleFor(a => a.ReducedAmount!.Value).GreaterThanOrEqualTo(0)
                    .PrecisionScale(18, 2, ignoreTrailingZeros: true)
                    .When(a => a.ReducedAmount.HasValue);
                article.RuleFor(a => a.ReducedAmount!.Value).LessThanOrEqualTo(a => a.Amount)
                    .When(a => a.ReducedAmount.HasValue)
                    .WithMessage("The пільгова amount cannot be above the full one.");
            });
        });

        When(c => c.Request.Articles is null, () =>
        {
            RuleFor(c => c.Request.StanytsiaFull).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
            RuleFor(c => c.Request.StanytsiaReduced).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
            RuleFor(c => c.Request.StanytsiaReduced).LessThanOrEqualTo(c => c.Request.StanytsiaFull)
                .WithMessage("The пільгова rate cannot be above the full one.");
            RuleFor(c => c.Request.KurinShare).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        });
    }
}

public sealed class SetKurinDuesRateCommandHandler : IRequestHandler<SetKurinDuesRateCommand, ServiceResult<object>>
{
    private readonly IDuesUnitOfWork _dues;
    private readonly ICurrentUserContext _currentUser;
    private readonly IKraiProfileProvider _profiles;

    public SetKurinDuesRateCommandHandler(IDuesUnitOfWork dues, ICurrentUserContext currentUser, IKraiProfileProvider profiles)
    {
        _dues = dues;
        _currentUser = currentUser;
        _profiles = profiles;
    }

    public async Task<ServiceResult<object>> Handle(SetKurinDuesRateCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey != request.KurinKey)
        {
            return new ServiceResult<object>(ResultType.Forbidden);
        }

        var from = new DuesQuarter(request.Request.FromQuarter.Year, request.Request.FromQuarter.Number).Index;
        var rates = await _dues.KurinDuesRates.GetForKurinAsync(request.KurinKey, cancellationToken);
        var existing = rates.FirstOrDefault(r => r.FromQuarter == from);
        var krai = await _profiles.DuesKraiForKurinAsync(request.KurinKey, cancellationToken);

        IReadOnlyList<KurinDuesArticle> articles;
        if (request.Request.Articles is { } sent)
        {
            articles = sent.Select(a => new KurinDuesArticle
            {
                Name = a.Name.Trim(),
                Recipient = a.Recipient,
                Amount = a.Amount,
                ReducedAmount = a.ReducedAmount ?? a.Amount
            }).ToList();

            // The край decides where its kurins hand money up; a стаття cannot send it elsewhere.
            if (articles.FirstOrDefault(a => !krai.Takes(a.Recipient)) is { } stray)
            {
                return ServiceResult<object>.Failure(ResultType.BadRequest, "DuesRecipientOff",
                    $"The край does not take вкладка that way: «{stray.Name}».");
            }
        }
        else
        {
            // Three amounts from a client that does not know статті: they would wipe the kurin's own.
            var inForce = rates.LastOrDefault(r => r.FromQuarter <= from);
            if (inForce is not null && !KurinDuesArticles.AreStandard(inForce.Articles))
            {
                return ServiceResult<object>.Failure(ResultType.Conflict, "DuesArticlesInUse",
                    "The kurin has its own статті of the вкладка; change them where they can be named.");
            }

            articles = KurinDuesArticles.Standard(
                request.Request.StanytsiaFull, request.Request.StanytsiaReduced, request.Request.KurinShare, paysKrai: !krai.Dues.PaysStanytsia);
        }

        if (existing is null)
        {
            var rate = new KurinDuesRate
            {
                KurinKey = request.KurinKey,
                FromQuarter = from,
                SetByUserKey = _currentUser.UserId
            };
            rate.SetArticles(articles);
            _dues.KurinDuesRates.Create(rate, cancellationToken);
        }
        else
        {
            existing.SetArticles(articles);
            existing.SetByUserKey = _currentUser.UserId;
            existing.UpdatedDate = DateTime.UtcNow;
            _dues.KurinDuesRates.Update(existing, cancellationToken);
        }

        await _dues.SaveChangesAsync(cancellationToken);
        return new ServiceResult<object>(ResultType.Success);
    }
}
