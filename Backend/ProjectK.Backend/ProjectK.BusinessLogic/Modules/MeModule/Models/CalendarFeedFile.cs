namespace ProjectK.BusinessLogic.Modules.MeModule.Models;

/// <summary>What the calendar link answers: a file, or nothing for a token that was never issued.</summary>
public sealed record CalendarFeedFile(string Content);
