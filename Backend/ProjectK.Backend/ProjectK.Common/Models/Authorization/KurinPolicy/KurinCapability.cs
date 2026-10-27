using System.Text.Json.Serialization;

namespace ProjectK.Common.Models.Authorization.KurinPolicy;

/// <summary>
/// One right a kurin sets for itself in «Права й видимість». Stored and sent by name, never by
/// number, so the values may be reordered or extended without touching saved policies.
/// What each one grants lives in <see cref="KurinCapabilities"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<KurinCapability>))]
public enum KurinCapability
{
    /// <summary>A member's email and phone.</summary>
    SeeContacts,

    /// <summary>A member's full date of birth, year included.</summary>
    SeeBirthDate,

    /// <summary>The day and month a member was born, without the year.</summary>
    SeeBirthday,

    /// <summary>A member's address and school.</summary>
    SeeAddress,

    /// <summary>The names and phones of a member's parents.</summary>
    SeeParents,

    /// <summary>The extra card fields a край adds.</summary>
    SeeCardExtras,

    /// <summary>Give a first пересторога.</summary>
    GiveWarning1,

    /// <summary>Give a second пересторога.</summary>
    GiveWarning2,

    /// <summary>Give a third пересторога.</summary>
    GiveWarning3,

    /// <summary>Cancel a пересторога someone else gave.</summary>
    CancelWarning,

    /// <summary>Confirm a first-level відзначення.</summary>
    ConfirmAward1,

    /// <summary>Confirm a second-level відзначення.</summary>
    ConfirmAward2,

    /// <summary>Confirm a third-level відзначення.</summary>
    ConfirmAward3,

    /// <summary>Confirm a fourth-level відзначення.</summary>
    ConfirmAward4,

    /// <summary>Delete a confirmed відзначення, or one someone else submitted.</summary>
    RemoveAward,

    /// <summary>Sign probe points, complete and verify a проба.</summary>
    SignProbe,

    /// <summary>Remove a signature and take a проба back from verified.</summary>
    UnsignProbe,

    /// <summary>Confirm or return a вмілість.</summary>
    ReviewBadge,

    /// <summary>Choose which edition of the probe programme a гурток walks.</summary>
    ChooseProbeEdition,

    /// <summary>Assign an event or a task to the whole kurin. Reaches the kurin or nobody.</summary>
    AssignKurin,

    /// <summary>Assign an event or a task to a гурток.</summary>
    AssignGroups,

    /// <summary>Assign an event or a task to one person.</summary>
    AssignMembers,

    /// <summary>Assign an event or a task to a провід: a гурток's, the курінний провід or the КВ.</summary>
    AssignProvid,

    /// <summary>Read the kurin's Журнал дій.</summary>
    ReadJournal
}
