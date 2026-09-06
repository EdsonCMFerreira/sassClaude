namespace sassClaude.Services;

public interface IEmailSender
{
    Task SendPasswordResetAsync(string recipient, string recipientName, string resetUrl, CancellationToken cancellationToken = default);

    Task SendEmailConfirmationAsync(string recipient, string recipientName, string confirmUrl, CancellationToken cancellationToken = default);

    Task SendWorkspaceInviteAsync(string recipient, string inviterName, string acceptUrl, CancellationToken cancellationToken = default);

    Task SendContactMessageAsync(string senderName, string senderEmail, string message, CancellationToken cancellationToken = default);
}