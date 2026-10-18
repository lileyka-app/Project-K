using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin.SetRate;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.DuesModule.Requests;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.KraiProfiles;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

/// <summary>The kurin's статті of the вкладка, and the three amounts of a client that does not know them.</summary>
public class SetKurinDuesRateTests
{
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly DuesQuarter Q1 = new(2026, 1);
    private static readonly DuesQuarter Q2 = new(2026, 2);

    private readonly List<KurinDuesRate> _rates = [];
    private readonly Mock<IDuesUnitOfWork> _dues = new();
    private readonly Mock<ICurrentUserContext> _user = new();
    private string _upstream = KraiDues.Stanytsia;

    public SetKurinDuesRateTests()
    {
        var rates = new Mock<IKurinDuesRateRepository>();
        rates.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _rates.OrderBy(r => r.FromQuarter).ToList());
        rates.Setup(r => r.Create(It.IsAny<KurinDuesRate>(), It.IsAny<CancellationToken>()))
            .Callback<KurinDuesRate, CancellationToken>((r, _) => _rates.Add(r));
        _dues.SetupGet(d => d.KurinDuesRates).Returns(rates.Object);
        _user.SetupGet(u => u.KurinKey).Returns(Kurin);
        _user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
    }

    private Task<Common.Models.Records.ServiceResult<object>> Send(SetKurinDuesRateRequest request) =>
        new SetKurinDuesRateCommandHandler(_dues.Object, _user.Object, KraiProfiles.Paying(Kurin, _upstream))
            .Handle(new SetKurinDuesRateCommand(Kurin, request), CancellationToken.None);

    private static QuarterRequest From(DuesQuarter q) => new() { Year = q.Year, Number = q.Number };

    private static DuesArticleRequest Article(string name, DuesRecipient recipient, decimal amount, decimal? reduced = null) =>
        new() { Name = name, Recipient = recipient, Amount = amount, ReducedAmount = reduced };

    [Fact]
    public async Task Articles_AreKeptInOrder_AndTheTotalsComeFromThem()
    {
        var result = await Send(new SetKurinDuesRateRequest
        {
            FromQuarter = From(Q1),
            Articles =
            [
                Article(" Станична вкладка ", DuesRecipient.Stanytsia, 240, 180),
                Article("Крайова вкладка", DuesRecipient.Stanytsia, 60),
                Article("Табірний фонд", DuesRecipient.Kurin, 50, 25)
            ]
        });

        result.Type.Should().Be(ResultType.Success);
        var rate = _rates.Single();
        rate.Articles.Select(a => a.Name).Should().Equal("Станична вкладка", "Крайова вкладка", "Табірний фонд");
        rate.Articles[1].ReducedAmount.Should().Be(60);
        rate.StanytsiaFull.Should().Be(300);
        rate.StanytsiaReduced.Should().Be(240);
        rate.KurinShare.Should().Be(50);
        rate.KurinReduced.Should().Be(25);
    }

    [Fact]
    public async Task ThreeAmounts_BecomeTheTwoStandardArticles()
    {
        await Send(new SetKurinDuesRateRequest { FromQuarter = From(Q1), StanytsiaFull = 240, StanytsiaReduced = 180, KurinShare = 15 });

        var rate = _rates.Single();
        KurinDuesArticles.AreStandard(rate.Articles).Should().BeTrue();
        rate.StanytsiaReduced.Should().Be(180);
        rate.KurinReduced.Should().Be(15);
    }

    [Fact]
    public async Task InAKraiPaidStraight_ThreeAmounts_SendTheUpperPartToTheKrai()
    {
        _upstream = KraiDues.Krai;

        await Send(new SetKurinDuesRateRequest { FromQuarter = From(Q1), StanytsiaFull = 600, StanytsiaReduced = 300, KurinShare = 100 });

        var rate = _rates.Single();
        rate.Articles.Select(a => (a.Name, a.Recipient)).Should().Equal(("Край", DuesRecipient.Krai), ("Курінь", DuesRecipient.Kurin));
        rate.KraiFull.Should().Be(600);
        rate.KraiReduced.Should().Be(300);
        rate.StanytsiaFull.Should().Be(0);
        KurinDuesArticles.AreStandard(rate.Articles).Should().BeTrue();
    }

    [Theory]
    [InlineData(KraiDues.Stanytsia, DuesRecipient.Krai, false)]
    [InlineData(KraiDues.Krai, DuesRecipient.Stanytsia, false)]
    [InlineData(KraiDues.Krai, DuesRecipient.Krai, true)]
    [InlineData(KraiDues.Both, DuesRecipient.Stanytsia, true)]
    [InlineData(KraiDues.Both, DuesRecipient.Krai, true)]
    public async Task AnArticle_GoesOnlyWhereTheKraiTakesTheVkladka(string upstream, DuesRecipient recipient, bool taken)
    {
        _upstream = upstream;

        var result = await Send(new SetKurinDuesRateRequest
        {
            FromQuarter = From(Q1),
            Articles = [Article("Внесок угору", recipient, 240), Article("Курінь", DuesRecipient.Kurin, 15)]
        });

        result.Type.Should().Be(taken ? ResultType.Success : ResultType.BadRequest);
        _rates.Should().HaveCount(taken ? 1 : 0);
    }

    [Fact]
    public async Task ThreeAmounts_DoNotWipeTheKurinsOwnArticles()
    {
        var own = new KurinDuesRate { KurinKey = Kurin, FromQuarter = Q1.Index };
        own.SetArticles([new KurinDuesArticle { Name = "Табірний фонд", Recipient = DuesRecipient.Kurin, Amount = 50, ReducedAmount = 50 }]);
        _rates.Add(own);

        var result = await Send(new SetKurinDuesRateRequest { FromQuarter = From(Q2), StanytsiaFull = 240, StanytsiaReduced = 180, KurinShare = 15 });

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("DuesArticlesInUse");
        _rates.Should().ContainSingle();
    }

    [Fact]
    public void Validator_TurnsDownTwoArticlesOfOneName_AndAPilhaAboveTheFullAmount()
    {
        var validator = new SetKurinDuesRateCommandValidator();

        validator.Validate(new SetKurinDuesRateCommand(Kurin, new SetKurinDuesRateRequest
        {
            FromQuarter = From(Q1),
            Articles = [Article("Курінь", DuesRecipient.Kurin, 15), Article("курінь ", DuesRecipient.Kurin, 10)]
        })).IsValid.Should().BeFalse();

        validator.Validate(new SetKurinDuesRateCommand(Kurin, new SetKurinDuesRateRequest
        {
            FromQuarter = From(Q1),
            Articles = [Article("Станиця", DuesRecipient.Stanytsia, 100, 150)]
        })).IsValid.Should().BeFalse();

        validator.Validate(new SetKurinDuesRateCommand(Kurin, new SetKurinDuesRateRequest
        {
            FromQuarter = From(Q1),
            Articles = []
        })).IsValid.Should().BeFalse();

        validator.Validate(new SetKurinDuesRateCommand(Kurin, new SetKurinDuesRateRequest
        {
            FromQuarter = From(Q1),
            Articles = [Article("Станиця", DuesRecipient.Stanytsia, 240, 180), Article("Курінь", DuesRecipient.Kurin, 15)]
        })).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_TurnsDownAMissingArticle_OrAMissingName_WithoutThrowing()
    {
        var validator = new SetKurinDuesRateCommandValidator();

        validator.Validate(new SetKurinDuesRateCommand(Kurin, new SetKurinDuesRateRequest
        {
            FromQuarter = From(Q1),
            Articles = [null!, Article("Курінь", DuesRecipient.Kurin, 15)]
        })).IsValid.Should().BeFalse();

        validator.Validate(new SetKurinDuesRateCommand(Kurin, new SetKurinDuesRateRequest
        {
            FromQuarter = From(Q1),
            Articles = [Article(null!, DuesRecipient.Kurin, 15)]
        })).IsValid.Should().BeFalse();
    }
}
