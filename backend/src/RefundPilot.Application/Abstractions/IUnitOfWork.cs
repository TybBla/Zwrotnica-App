namespace RefundPilot.Application.Abstractions;

/// <summary>Runs several repository writes (possibly touching more than one table) atomically.</summary>
public interface IUnitOfWork
{
    Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct);
}
