using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

public class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string User { get; set; } = "";
    public string Pass { get; set; } = "";
    public string FromName { get; set; } = "Jade服飾電商";
    public string FromEmail { get; set; } = "";
}

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = false);
}

public class EmailService : IEmailService
{
    private readonly SmtpSettings _smtpSettings;

    public EmailService(IOptions<SmtpSettings> smtpSettings)
    {
        _smtpSettings = smtpSettings.Value;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = false)
    {
        using var mail = new MailMessage();
        mail.From = new MailAddress(_smtpSettings.FromEmail, _smtpSettings.FromName);
        mail.To.Add(toEmail);
        mail.Subject = subject;
        mail.Body = body;
        mail.IsBodyHtml = isHtml;

        using var smtp = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port);
        smtp.Credentials = new NetworkCredential(_smtpSettings.User, _smtpSettings.Pass);
        smtp.EnableSsl = true;

        await smtp.SendMailAsync(mail);
    }
}
