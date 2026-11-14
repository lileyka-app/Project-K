using MediatR;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;

public class DeleteKurinCommand : IRequest<ServiceResult<Unit>>
{
    public Guid KurinKey { get; set; }
    public DeleteKurinCommand(Guid kurinKey)
    {
        KurinKey = kurinKey;
    }
}

public class DeleteKurinCommandHandler : IRequestHandler<DeleteKurinCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackendCache _cache;
    public DeleteKurinCommandHandler(IUnitOfWork unitOfWork, IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }
    public async Task<ServiceResult<Unit>> Handle(DeleteKurinCommand request, CancellationToken cancellationToken)
    {
        if (request.KurinKey == Guid.Empty)
        {
            return ServiceResult<Unit>.Failure(
                ResultType.BadRequest,
                KurinErrors.KurinKeyRequired,
                "KurinKey cannot be empty.");
        }

        var existing = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);

        if (existing is null)
        {
            return ServiceResult<Unit>.Failure(
                ResultType.NotFound,
                KurinErrors.KurinNotFound,
                $"Kurin with key {request.KurinKey} not found.");
        }

        // The people stay. This is what the release is for: a kurin closing is something that
        // happens to a kurin, not to the person who belonged to it, and everything they earned —
        // levels, вмілості, probes, awards, перестороги — is theirs and stays with them, stamped
        // with the kurin it happened in. They simply end up belonging nowhere, until someone
        // takes them into another kurin.
        //
        // What the database will not clear itself: offices are NO ACTION against both the kurin
        // and its гуртки, and the гуртки's own cascade is refused while an office still points at
        // one. Everything else — гуртки, agenda with its assignments, planning sessions, mentor
        // assignments — cascades.
        await _unitOfWork.Leaderships.DeleteForKurinAsync(request.KurinKey, cancellationToken);

        // Membership rows are the kurin's own record of who was in it, and they name it by a
        // foreign key, so they go with it. Nothing about the people goes with them.
        await _unitOfWork.Memberships.RemoveForKurinAsync(request.KurinKey, cancellationToken);

        // Accounts remember which kurin they stood in, and nothing in the schema forgets it for
        // them. Left alone, everyone who had stepped into this kurin would keep signing in to a
        // key that no longer exists — shown a kurin, offered no way out of it (STAB-05).
        await _unitOfWork.Users.DetachFromKurinAsync(request.KurinKey, cancellationToken);

        _unitOfWork.Kurins.Delete(existing, cancellationToken);

        var changes = await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (changes <= 0)
        {
            return ServiceResult<Unit>.Failure(
                ResultType.InternalServerError,
                KurinErrors.KurinDeleteFailed,
                "Failed to delete Kurin due to internal error.");
        }

        _cache.Invalidate(BackendCachePolicies.KurinReads);
        _cache.Invalidate(BackendCachePolicies.GroupReads);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
