using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KraiModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Services;

internal static class KraiNames
{
    public const int MaxLength = 120;

    /// <summary>
    /// Whether another край already goes by this name. Pickers show names only, so two краї with one
    /// name could not be told apart.
    /// </summary>
    public static async Task<bool> TakenAsync(IUnitOfWork unitOfWork, string name, string? except, CancellationToken cancellationToken)
    {
        return (await unitOfWork.Krais.GetAllAsync(cancellationToken))
            .Any(k => k.Code != except && string.Equals(k.Name.Trim(), name, StringComparison.CurrentCultureIgnoreCase));
    }

    public static ServiceResult<KraiDto> Taken(string name) =>
        ServiceResult<KraiDto>.Failure(ResultType.Conflict, KraiErrors.KraiNameTaken, $"A krai named '{name}' already exists.");

    public static ServiceResult<KraiDto> Invalid() =>
        ServiceResult<KraiDto>.Failure(ResultType.BadRequest, KraiErrors.InvalidKraiName, $"A krai name is 1 to {MaxLength} characters.");

    public static string? Clean(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength ? null : trimmed;
    }
}
