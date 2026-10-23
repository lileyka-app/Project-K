namespace ProjectK.Common.Models.Enums;

/// <summary>What waits on a reviewer, which decides who is told about it.</summary>
public enum ReviewSubject
{
    /// <summary>A profile that needs checking again: whoever may update the member.</summary>
    Profile,

    /// <summary>A відзначення to confirm: whoever the kurin's policy lets confirm one.</summary>
    Award,

    /// <summary>A вмілість to review: whoever the kurin's policy lets review one.</summary>
    Badge
}
