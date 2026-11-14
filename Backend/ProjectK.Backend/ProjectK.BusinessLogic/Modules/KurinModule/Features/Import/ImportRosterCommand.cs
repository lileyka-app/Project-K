using MediatR;
using ProjectK.BusinessLogic.Behaviors;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Import;

/// <summary>
/// Takes a roster into a kurin. Ran twice by design: once with <paramref name="DryRun"/> to show
/// the провід what would happen, then again to do it. The rows come back from the preview rather
/// than from a second upload — the file is read once, and what is confirmed is exactly what was
/// shown.
/// </summary>
/// <param name="CreateMissingGroups">
/// Гуртки named in the file that the kurin does not have. Without this they are a reason to
/// reject the row, so nobody quietly ends up in a гурток nobody meant to open.
/// </param>
public sealed record ImportRosterCommand(
    Guid KurinKey,
    IReadOnlyList<SheetRow> Rows,
    IReadOnlyList<ColumnMapping> Mapping,
    bool CreateMissingGroups,
    bool DryRun) : IRequest<ServiceResult<RosterImportReport>>, ITransactionalRequest;

/// <summary>What the import would do, or did, with one row.</summary>
public enum RowOutcome
{
    /// <summary>A person the system has never seen. A member record is opened.</summary>
    Created,

    /// <summary>Someone already here. They are taken into the kurin, not duplicated.</summary>
    Attached,

    /// <summary>Already in this kurin. Nothing to do, and not an error.</summary>
    AlreadyHere,

    Rejected
}

/// <summary>One row's verdict, with the row number the person sees in Excel.</summary>
/// <param name="AccountInvited">
/// Whether an account was opened for the person and an activation letter sent to the row's
/// address (on a dry run: whether one would be).
/// </param>
public sealed record RowResult(
    int RowNumber,
    string Name,
    RowOutcome Outcome,
    string? Reason = null,
    bool AccountInvited = false);

/// <summary>
/// The whole picture before or after writing: what happens to each row, which гуртки would have
/// to be opened, and which rows name a different kurin than the one being imported into.
/// </summary>
public sealed record RosterImportReport(
    IReadOnlyList<RowResult> Rows,
    IReadOnlyList<string> MissingGroups,
    IReadOnlyList<int> ForeignKurinRows,
    bool DryRun)
{
    public int CreatedCount => Rows.Count(row => row.Outcome == RowOutcome.Created);

    public int AttachedCount => Rows.Count(row => row.Outcome == RowOutcome.Attached);

    public int AlreadyHereCount => Rows.Count(row => row.Outcome == RowOutcome.AlreadyHere);

    public int RejectedCount => Rows.Count(row => row.Outcome == RowOutcome.Rejected);

    public int InvitedCount => Rows.Count(row => row.AccountInvited);
}
