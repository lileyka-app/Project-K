using System;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.SubmitWaitlistRegistration;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.Settings;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.AuthModule.HandlerTests.Onboarding;

public class SubmitWaitlistRegistrationCommandValidatorTests
{
    private readonly SubmitWaitlistRegistrationCommandValidator _validator = new(Options.Create(new KurinNumberOptions()), UnitOfWorkKnowingPoland());

    [Fact]
    public void Validate_ShouldPass_ForValidCommand()
    {
        var result = _validator.TestValidate(CreateCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenStanytsiaMissing(string? stanytsia)
    {
        var result = _validator.TestValidate(CreateCommand(stanytsia: stanytsia));
        result.ShouldHaveValidationErrorFor(x => x.Stanytsia).WithErrorMessage("Stanytsia is required.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenStanytsiaTooLong()
    {
        var result = _validator.TestValidate(CreateCommand(stanytsia: new string('x', 121)));
        result.ShouldHaveValidationErrorFor(x => x.Stanytsia).WithErrorMessage("Stanytsia must be 120 characters or fewer.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_ShouldFail_WhenRegionMissing(string? region)
    {
        var result = _validator.TestValidate(CreateCommand(regionOrCountry: region));
        result.ShouldHaveValidationErrorFor(x => x.RegionOrCountry).WithErrorMessage("Region is required.");
    }

    [Fact]
    public async Task Validate_ShouldPass_WhenAKnownKraiStandsInForTheRegion()
    {
        var result = await _validator.TestValidateAsync(CreateCommand(regionOrCountry: null, kraiCode: " PL "));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenTheKraiIsUnknown()
    {
        var result = await _validator.TestValidateAsync(CreateCommand(kraiCode: "zz"));
        result.ShouldHaveValidationErrorFor(x => x.KraiCode).WithErrorCode("KraiNotFound");
    }

    [Fact]
    public void Validate_ShouldFail_WhenKurinLeaderCandidateNotConfirmed()
    {
        var result = _validator.TestValidate(CreateCommand(isKurinLeaderCandidate: false));
        result.ShouldHaveValidationErrorFor(x => x.IsKurinLeaderCandidate)
            .WithErrorMessage("Kurin leader confirmation is required.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_ShouldFail_WhenKurinNumberMissing(string? number)
    {
        var result = _validator.TestValidate(CreateCommand(claimedKurinNumber: number));
        result.ShouldHaveValidationErrorFor(x => x.ClaimedKurinNameOrNumber)
            .WithErrorMessage("Kurin number is required.");
    }

    [Theory]
    [InlineData("97a")]
    [InlineData("Lisovi Chorty")]
    public void Validate_ShouldFail_WhenKurinNumberNotNumeric(string number)
    {
        var result = _validator.TestValidate(CreateCommand(claimedKurinNumber: number));
        result.ShouldHaveValidationErrorFor(x => x.ClaimedKurinNameOrNumber)
            .WithErrorMessage("Kurin number must contain only digits.");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("201")]
    [InlineData("1153")]
    public void Validate_ShouldFail_WhenKurinNumberOutsideTheRange(string number)
    {
        var result = _validator.TestValidate(CreateCommand(claimedKurinNumber: number));
        result.ShouldHaveValidationErrorFor(x => x.ClaimedKurinNameOrNumber)
            .WithErrorMessage("Kurin number must be between 1 and 200.");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("200")]
    public void Validate_ShouldPass_AtTheEdgesOfTheRange(string number)
    {
        var result = _validator.TestValidate(CreateCommand(claimedKurinNumber: number));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_ShouldPass_ForPreparatoryKurinWithoutNumber(string? number)
    {
        var result = _validator.TestValidate(CreateCommand(claimedKurinNumber: number, isPreparatoryKurin: true));
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static SubmitWaitlistRegistrationCommand CreateCommand(
        string? stanytsia = "Kyiv",
        string? regionOrCountry = "Ukraine",
        bool isKurinLeaderCandidate = true,
        string? claimedKurinNumber = "97",
        bool isPreparatoryKurin = false,
        string? kraiCode = null)
        => new(
            "Ihor",
            "Kovalenko",
            "ihor.kovalenko@example.com",
            "+38 (099) 111-22-33",
            new DateTime(1995, 5, 15),
            stanytsia,
            regionOrCountry,
            isKurinLeaderCandidate,
            claimedKurinNumber,
            isPreparatoryKurin,
            kraiCode);

    private static IUnitOfWork UnitOfWorkKnowingPoland()
    {
        var krais = new Mock<IKraiRepository>();
        krais.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) => code == Krai.Poland);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.Krais).Returns(krais.Object);
        return unitOfWork.Object;
    }
}
