using FluentAssertions;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member;
using ProjectK.Common.Dtos.KraiModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Records.KraiModule;
using ProjectK.Common.Rules.KraiModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule;

public class MemberCardFieldsTests
{
    private static readonly KraiCardFieldsSettings Prague = new(
        new Dictionary<string, string>
        {
            [KraiCardFields.MiddleName] = KraiCardFields.Hidden,
            [KraiCardFields.PhoneNumber] = KraiCardFields.Optional
        },
        [
            new KraiCardExtraField { Key = "visaType", Label = "Тип візи", Required = true },
            new KraiCardExtraField { Key = "visaUntil", Label = "Віза до", Type = KraiCardFields.Date }
        ]);

    private static UpsertMemberProfileCommand Write(Dictionary<string, string>? extras = null, string middleName = "", string phone = "") => new()
    {
        FirstName = "Ivan",
        LastName = "Petrenko",
        MiddleName = middleName,
        PhoneNumber = phone,
        CardExtras = extras
    };

    private static Member Existing(string? cardExtras) => new()
    {
        FirstName = "Ivan",
        LastName = "Petrenko",
        MiddleName = "Ivanovych",
        Email = "ivan@example.com",
        PhoneNumber = "+420 777 000 111",
        Address = "Praha 3, Seifertova 1",
        School = "ZŠ Praha 3",
        MotherFirstName = "Olena",
        MotherLastName = "Petrenko",
        MotherPhone = "+420777000222",
        CardExtras = cardExtras
    };

    [Fact]
    public void WithoutAProfile_MiddleNameAndPhone_StayRequired()
    {
        var outcome = MemberCardFields.Apply(KraiCardFieldsSettings.Default, Write(middleName: "I"), null);

        outcome.Error.Should().Be("Поле «Номер телефону» обовʼязкове.");
    }

    [Fact]
    public void AHiddenField_KeepsWhatTheMemberHad()
    {
        var request = Write(new() { ["visaType"] = "D" }, middleName: "Changed");

        var outcome = MemberCardFields.Apply(Prague, request, Existing(null));

        outcome.Error.Should().BeNull();
        request.MiddleName.Should().Be("Ivanovych");
    }

    [Fact]
    public void AnOptionalPhoneLeftEmpty_IsStoredAsEmpty()
    {
        var request = Write(new() { ["visaType"] = "D" });
        request.PhoneNumber = null!;

        var outcome = MemberCardFields.Apply(Prague, request, null);

        outcome.Error.Should().BeNull();
        request.PhoneNumber.Should().BeEmpty();
    }

    [Fact]
    public void ARequiredExtraField_MustBeFilled()
    {
        var outcome = MemberCardFields.Apply(Prague, Write(new() { ["visaType"] = "  " }), null);

        outcome.Error.Should().Be("Поле «Тип візи» обовʼязкове.");
    }

    [Fact]
    public void ExtraValues_AreMerged_AndAFieldTheKraiDroppedIsKept()
    {
        var outcome = MemberCardFields.Apply(
            Prague,
            Write(new() { ["visaUntil"] = "2027-06-30", ["unknown"] = "x" }),
            Existing("""{"visaType":"D","oldField":"kept"}"""));

        outcome.Error.Should().BeNull();
        MemberCardExtras.Read(outcome.CardExtras).Should().Equal(new Dictionary<string, string>
        {
            ["oldField"] = "kept",
            ["visaType"] = "D",
            ["visaUntil"] = "2027-06-30"
        });
    }

    [Fact]
    public void ADateField_TakesOnlyADate()
    {
        var outcome = MemberCardFields.Apply(Prague, Write(new() { ["visaType"] = "D", ["visaUntil"] = "30.06.2027" }), null);

        outcome.Error.Should().Be("Поле «Віза до» має бути датою.");
    }

    [Fact]
    public void AnEmptyValue_ClearsTheField()
    {
        var outcome = MemberCardFields.Apply(
            Prague,
            Write(new() { ["visaUntil"] = "" }),
            Existing("""{"visaType":"D","visaUntil":"2027-06-30"}"""));

        MemberCardExtras.Read(outcome.CardExtras).Should().Equal(new Dictionary<string, string> { ["visaType"] = "D" });
    }

    [Fact]
    public void AddressAndSchoolLeftOut_KeepWhatTheMemberHad()
    {
        var request = Write(new() { ["visaType"] = "D" });

        var outcome = MemberCardFields.Apply(Prague, request, Existing(null));

        outcome.Error.Should().BeNull();
        request.Address.Should().Be("Praha 3, Seifertova 1");
        request.School.Should().Be("ZŠ Praha 3");
    }

    [Fact]
    public void AddressSentEmpty_IsCleared_AndSchoolSent_IsTrimmed()
    {
        var request = Write(new() { ["visaType"] = "D" });
        request.Address = "";
        request.School = "  Gymnázium  ";

        var outcome = MemberCardFields.Apply(Prague, request, Existing(null));

        outcome.Error.Should().BeNull();
        request.Address.Should().BeNull();
        request.School.Should().Be("Gymnázium");
    }

    [Fact]
    public void AHiddenAddressAndParents_KeepWhatTheMemberHad()
    {
        var card = new KraiCardFieldsSettings(
            new Dictionary<string, string>
            {
                [KraiCardFields.Address] = KraiCardFields.Hidden,
                [KraiCardFields.Parents] = KraiCardFields.Hidden
            }, []);
        var request = Write(middleName: "I", phone: "+420777000111");
        request.Address = "Somewhere else";
        request.MotherFirstName = "Someone";

        MemberCardFields.Apply(card, request, Existing(null)).Error.Should().BeNull();

        request.Address.Should().Be("Praha 3, Seifertova 1");
        request.MotherFirstName.Should().Be("Olena");
    }

    [Fact]
    public void ARequiredSchool_MustBeFilled()
    {
        var card = new KraiCardFieldsSettings(
            new Dictionary<string, string> { [KraiCardFields.School] = KraiCardFields.Required }, []);
        var request = Write(middleName: "I", phone: "+420777000111");
        request.School = " ";

        MemberCardFields.Apply(card, request, null).Error.Should().Be("Поле «Школа» обовʼязкове.");
    }

    [Fact]
    public void AddressAndSchool_AreOptionalWithoutAProfile_AndAKurinMayHideThem()
    {
        KraiCardFieldsSettings.Default.ModeOf(KraiCardFields.Address).Should().Be(KraiCardFields.Optional);
        KraiCardFieldsSettings.Default.Hideable().Select(field => field.Key)
            .Should().BeEquivalentTo([KraiCardFields.Address, KraiCardFields.School, KraiCardFields.Parents]);
    }

    [Fact]
    public void ParentsLeftOut_KeepWhatTheMemberHad_AndAnEmptyValueClears()
    {
        var request = Write(new() { ["visaType"] = "D" });
        request.MotherPhone = "";
        request.FatherFirstName = " Petro ";

        MemberCardFields.Apply(Prague, request, Existing(null)).Error.Should().BeNull();

        request.MotherFirstName.Should().Be("Olena");
        request.MotherPhone.Should().BeNull();
        request.FatherFirstName.Should().Be("Petro");
        request.FatherPhone.Should().BeNull();
    }

    [Fact]
    public void RequiredParents_NeedAtLeastOnePhone()
    {
        var card = new KraiCardFieldsSettings(
            new Dictionary<string, string> { [KraiCardFields.Parents] = KraiCardFields.Required }, []);
        var request = Write(middleName: "I", phone: "+420777000111");
        request.MotherFirstName = "Olena";

        MemberCardFields.Apply(card, request, null).Error.Should().Be("Поле «Контакти батьків» обовʼязкове.");

        request.FatherPhone = "+420777000333";
        MemberCardFields.Apply(card, request, null).Error.Should().BeNull();
    }
}
