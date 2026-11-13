namespace RefundPilot.Application.Abstractions;

public interface ICurrentUser
{
    string? UserId { get; }
    string? Email { get; }
}
