namespace sassClaude.Services;

public interface IEmailSender
{
    Task SendPasswordResetAsync(string recipient, string recipientName, string resetUrl, CancellationToken cancellationToken = default);
}