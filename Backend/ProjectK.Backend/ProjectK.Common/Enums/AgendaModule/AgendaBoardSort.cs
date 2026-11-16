namespace ProjectK.Common.Enums.AgendaModule;

/// <summary>How the board orders a column.</summary>
public enum AgendaBoardSort
{
    /// <summary>Nearest deadline first; tasks without one last.</summary>
    Due = 0,
    /// <summary>Last touched first.</summary>
    Recent = 1,
    /// <summary>Newest first.</summary>
    Created = 2,
    Title = 3
}
