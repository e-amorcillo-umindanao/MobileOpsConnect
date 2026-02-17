using System.Net;
using System.Net.Mail;

namespace MobileOpsConnect.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                var host = _config["Smtp:Host"] ?? "smtp.gmail.com";
                var port = int.Parse(_config["Smtp:Port"] ?? "587");
                var username = _config["Smtp:Username"] ?? "";
                var password = _config["Smtp:Password"] ?? "";
                var fromEmail = _config["Smtp:FromEmail"] ?? username;
                var fromName = _config["Smtp:FromName"] ?? "MobileOps Connect";

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    _logger.LogWarning("SMTP credentials not configured. Email not sent to {ToEmail}", toEmail);
                    return;
                }

                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl = true
                };

                var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(toEmail);

                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent to {ToEmail}: {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {ToEmail}", toEmail);
            }
        }
    }
}
