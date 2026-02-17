namespace MobileOpsConnect.Services
{
    public interface IEmailService
    {
        /// <summary>
        /// Send an email to a specific recipient.
        /// </summary>
        Task SendEmailAsync(string toEmail, string subject, string htmlBody);
    }
}
