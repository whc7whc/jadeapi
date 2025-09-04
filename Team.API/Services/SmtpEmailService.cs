using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Team.API.Services
{
    public class SmtpEmailService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPass;

        public SmtpEmailService(string smtpHost, int smtpPort, string smtpUser, string smtpPass)
        {
            _smtpHost = smtpHost;
            _smtpPort = smtpPort;
            _smtpUser = smtpUser;
            _smtpPass = smtpPass;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var message = new MailMessage();
            message.From = new MailAddress("tainanjade@gmail.com", "JADE 電商服飾", System.Text.Encoding.UTF8);
            message.To.Add(toEmail);
            message.Subject = subject;
            message.SubjectEncoding = System.Text.Encoding.UTF8;
            message.Body = body;
            message.BodyEncoding = System.Text.Encoding.UTF8;
            message.IsBodyHtml = true; // 🔑 確保 HTML 被解析

            using (var client = new SmtpClient("smtp.gmail.com", 587))
            {
                client.Credentials = new NetworkCredential("tainanjade@gmail.com", "izkb nhjp ilvm tmbi");
                client.EnableSsl = true;
                await client.SendMailAsync(message);
            }
        }

    }
}
