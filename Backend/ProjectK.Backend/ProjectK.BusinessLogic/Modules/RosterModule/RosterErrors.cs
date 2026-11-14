using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.RosterModule;

/// <summary>
/// The codes a roster import or a registry export fails with. The client picks its wording by them,
/// so a code is a contract: <see cref="KurinNotFound"/> is the same string the kurin module answers
/// with, because the client reads it the same way whichever module said it.
/// </summary>
public static class RosterErrors
{
    public const string KurinNotFound = "KurinNotFound";

    /// <summary>A senior kurin has no гуртки to import a roster into.</summary>
    public const string RosterImportNotForSeniorKurin = "RosterImportNotForSeniorKurin";

    /// <summary>The roster upload carried no file.</summary>
    public const string NoFile = "NoFile";

    public const string EmptyFile = "EmptyFile";
    public const string UnreadableFile = "UnreadableFile";

    public static ServiceResult<T> KurinMissing<T>() =>
        ServiceResult<T>.Failure(ResultType.NotFound, KurinNotFound, "Kurin was not found.");
}
