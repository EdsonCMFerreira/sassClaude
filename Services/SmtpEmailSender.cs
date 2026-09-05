using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace sassClaude.Services;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendPasswordResetAsync(string recipient, string recipientName, string resetUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.From))
        {
            _logger.LogWarning("SMTP não configurado. Link de recuperação para {Email}: {ResetUrl}", recipient, resetUrl);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.From));
        message.To.Add(new MailboxAddress(recipientName, recipient));
        message.Subject = "Redefinição de senha | sassClaude";
        message.Body = new BodyBuilder
        {
            HtmlBody = $"<p>Olá, {System.Net.WebUtility.HtmlEncode(recipientName)}.</p><p>Recebemos uma solicitação para redefinir sua senha.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(resetUrl)}\">Redefinir minha senha</a></p><p>O link expira em 30 minutos. Se você não solicitou isso, ignore este e-mail.</p>"
        }.ToMessageBody();

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.StartTls, cancellationToken);
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await smtp.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        }

        await smtp.SendAsync(message, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);
    }
}