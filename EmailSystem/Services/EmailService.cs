using EmailSystem.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace EmailSystem.Services;

/// <summary>
/// Envia e-mails via SMTP usando MailKit/MimeKit.
/// </summary>
public class EmailService
{
    private readonly SmtpConfig _config;

    public EmailService(SmtpConfig config)
    {
        _config = config;
    }

    public async Task SendAsync(EmailMessage message)
    {
        var mime = new MimeMessage();
        // Usa o From da mensagem se definido, senão usa o username do SMTP
        string fromAddr = string.IsNullOrEmpty(message.From) ? _config.Username : message.From;
        mime.From.Add(MailboxAddress.Parse(fromAddr));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;

        var builder = new BodyBuilder();
        if (message.IsHtml)
            builder.HtmlBody = message.Body;
        else
            builder.TextBody = message.Body;

        foreach (var path in message.Attachments)
            if (File.Exists(path))
                await builder.Attachments.AddAsync(path);

        mime.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_config.Host, _config.Port,
            _config.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
        await client.AuthenticateAsync(_config.Username, _config.Password);
        await client.SendAsync(mime);
        await client.DisconnectAsync(true);
    }
}
