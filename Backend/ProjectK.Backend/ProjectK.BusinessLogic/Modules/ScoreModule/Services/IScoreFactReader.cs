using ProjectK.Common.Records.ScoreModule;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>
/// The facts other modules know that are worth points — a вмілість confirmed, a quarter of вкладка
/// paid, a пересторога in force. The ledger takes them as it takes marks; where they come from is
/// this seam's business.
/// </summary>
public interface IScoreFactReader
{
    Task<IReadOnlyList<ScoreFact>> ForKurinAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}
