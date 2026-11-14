using System.ComponentModel.DataAnnotations;

namespace ProjectK.Common.Enums.AuthModule;

public enum WaitlistEntrySource
{
    /// <summary>Submitted through the public waitlist form.</summary>
    Application = 0,

    /// <summary>Opened on someone's behalf when a провід gave a member an account directly.</summary>
    DirectAdd = 1
}
