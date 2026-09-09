using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Saas.Services;

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
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("SMTP não configurado. Link de recuperação para {Email}: {ResetUrl}", recipient, resetUrl);
            return;
        }

        var html = $"<p>Olá, {System.Net.WebUtility.HtmlEncode(recipientName)}.</p><p>Recebemos uma solicitação para redefinir sua senha.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(resetUrl)}\">Redefinir minha senha</a></p><p>O link expira em 30 minutos. Se você não solicitou isso, ignore este e-mail.</p>";
        await SendAsync(recipient, recipientName, "Redefinição de senha | Saas", html, cancellationToken);
    }

    public async Task SendEmailConfirmationAsync(string recipient, string recipientName, string confirmUrl, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("SMTP não configurado. Link de confirmação de e-mail para {Email}: {ConfirmUrl}", recipient, confirmUrl);
            return;
        }

        var html = $"<p>Olá, {System.Net.WebUtility.HtmlEncode(recipientName)}.</p><p>Confirme seu e-mail para ativar sua conta no Saas.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(confirmUrl)}\">Confirmar meu e-mail</a></p><p>O link expira em 30 minutos.</p>";
        await SendAsync(recipient, recipientName, "Confirme seu e-mail | Saas", html, cancellationToken);
    }

    public async Task SendWorkspaceInviteAsync(string recipient, string inviterName, string acceptUrl, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("SMTP não configurado. Link de convite para {Email}: {AcceptUrl}", recipient, acceptUrl);
            return;
        }

        var html = $"<p>{System.Net.WebUtility.HtmlEncode(inviterName)} convidou você para o workspace no Saas.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(acceptUrl)}\">Aceitar convite e criar conta</a></p><p>O convite expira em 7 dias.</p>";
        await SendAsync(recipient, recipient, "Você foi convidado para um workspace | Saas", html, cancellationToken);
    }

    public async Task SendContactMessageAsync(string senderName, string senderEmail, string message, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("SMTP não configurado. Mensagem de contato de {Name} <{Email}>: {Message}", senderName, senderEmail, message);
            return;
        }

        var html = $"<p>Nova mensagem de contato de {System.Net.WebUtility.HtmlEncode(senderName)} ({System.Net.WebUtility.HtmlEncode(senderEmail)}):</p><p>{System.Net.WebUtility.HtmlEncode(message)}</p>";
        await SendAsync(_options.From, "Suporte Saas", "Nova mensagem de contato | Saas", html, cancellationToken, replyTo: senderEmail);
    }

    private async Task SendAsync(string recipient, string recipientName, string subject, string html, CancellationToken cancellationToken, string? replyTo = null)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.From));
        message.To.Add(new MailboxAddress(recipientName, recipient));
        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        }

        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

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