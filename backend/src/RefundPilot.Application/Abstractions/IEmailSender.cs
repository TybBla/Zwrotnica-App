namespace RefundPilot.Application.Abstractions;

/// <summary>Sends one e-mail with a plain-text body and an HTML alternative. Throws when it could not be handed over.</summary>
public interface IEmailSender
{
    /// <exception cref="EmailRecipientRejectedException">This recipient will never accept it: do not retry.</exception>
    /// <exception cref="EmailTransportException">The mail server is unreachable or refuses us: stop the run.</exception>
    Task SendAsync(string to, string subject, string textBody, string htmlBody, CancellationToken ct);
}

public enum EmailRejection
{
    /// <summary>A reserved test domain (.test, example.com, ...) that real SMTP must never be asked to deliver to.</summary>
    ReservedDomain,
    InvalidAddress,
    /// <summary>The server refused the mailbox permanently (5xx).</summary>
    RecipientRefused,
}

/// <summary>Permanent failure for one recipient. The message holds the reason only, never the address.</summary>
public sealed class EmailRecipientRejectedException(EmailRejection reason, Exception? inner = null)
    : Exception($"Recipient rejected: {reason}.", inner)
{
    public EmailRejection Reason { get; } = reason;
}

/// <summary>The mail server cannot be used at all right now (connection, TLS, authentication, timeout).</summary>
public sealed class EmailTransportException(string message, Exception? inner = null) : Exception(message, inner);
