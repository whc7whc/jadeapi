namespace Team.Backend.Services
{
    public interface IUserEmailSender
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
    }
}
