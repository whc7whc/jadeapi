using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging; // 若要記錄錯誤 log

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
    private readonly ILogger<EmailService>? _logger;

    public EmailService(IOptions<SmtpSettings> smtpSettings, ILogger<EmailService>? logger = null)
    {
        _smtpSettings = smtpSettings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = false)
    {
        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(_smtpSettings.FromEmail, _smtpSettings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            mail.To.Add(toEmail);

            using var smtp = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port)
            {
                Credentials = new NetworkCredential(_smtpSettings.User, _smtpSettings.Pass),
                EnableSsl = true,
                Timeout = 10000 // 🔴 關鍵：設定 10 秒 timeout，防止長時間卡住
            };

            await smtp.SendMailAsync(mail);
        }
        catch (SmtpException smtpEx)
        {
            _logger?.LogError(smtpEx, "SMTP 發送錯誤：{Message}", smtpEx.Message);
            throw new InvalidOperationException("寄送 Email 時發生 SMTP 錯誤，請稍後再試。", smtpEx);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Email 發送失敗：{Message}", ex.Message);
            throw new InvalidOperationException("寄送 Email 失敗，請稍後再試。", ex);
        }
    }
}
