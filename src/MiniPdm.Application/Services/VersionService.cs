using MiniPdm.Application.Interfaces;
using MiniPdm.Domain.Enums;

namespace MiniPdm.Application.Services;

public sealed class VersionService : IVersionService
{
    private readonly IPdmRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public VersionService(IPdmRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task ChangeStateAsync(Guid versionId, ObjectState newState, CancellationToken ct)
    {
        var version = await _repository.GetVersionAsync(versionId, ct)
            ?? throw new InvalidOperationException("Версия не найдена.");

        var allowed = false;
        if (version.State == ObjectState.InWork && newState == ObjectState.Approved) allowed = true;
        if (version.State == ObjectState.InWork && newState == ObjectState.Cancelled) allowed = true;
        if (version.State == ObjectState.Approved && newState == ObjectState.Cancelled) allowed = true;

        if (!allowed)
            throw new InvalidOperationException($"Переход из состояния «{version.State}» в «{newState}» запрещён.");

        await _unitOfWork.BeginAsync(ct);
        try
        {
            await _repository.UpdateStateAsync(version.Id, newState, ct);

            if (newState == ObjectState.Cancelled)
            {
                var current = await _repository.GetLatestNonCancelledVersionAsync(version.ObjectId, ct);
                await _repository.SetCurrentVersionAsync(version.ObjectId, current?.Id, ct);
            }
            else
            {
                await _repository.SetCurrentVersionAsync(version.ObjectId, version.Id, ct);
            }

            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
