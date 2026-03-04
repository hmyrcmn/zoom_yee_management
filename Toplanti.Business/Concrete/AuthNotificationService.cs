using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;
using Toplanti.Business.Abstract;

namespace Toplanti.Business.Concrete
{
    public class AuthNotificationService : IAuthNotificationService
    {
        private readonly IConfiguration _configuration;

        public AuthNotificationService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> SendOtpCode(string email, string code)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            var host = (_configuration["Otp:Smtp:Host"] ?? string.Empty).Trim();
            var username = (_configuration["Otp:Smtp:Username"] ?? string.Empty).Trim();
            var password = _configuration["Otp:Smtp:Password"] ?? string.Empty;
            var fromEmail = (_configuration["Otp:Smtp:FromEmail"] ?? string.Empty).Trim();
            var fromName = (_configuration["Otp:Smtp:FromName"] ?? "YEE Toplanti").Trim();
            var allowConsoleFallback = _configuration.GetValue<bool>("Otp:AllowConsoleFallback");
            var port = _configuration.GetValue<int?>("Otp:Smtp:Port") ?? 587;

            if (string.IsNullOrWhiteSpace(host)
                || string.IsNullOrWhiteSpace(username)
                || string.IsNullOrWhiteSpace(password)
                || string.IsNullOrWhiteSpace(fromEmail))
            {
                if (allowConsoleFallback)
                {
                    Console.WriteLine($"[OTP:FALLBACK] {email} icin kod: {code}");
                    return true;
                }

                return false;
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(MailboxAddress.Parse(email.Trim()));
                message.Subject = "YEE Toplanti Giris Dogrulama Kodu";
                message.Body = new TextPart("plain")
                {
                    Text = $"Giris dogrulama kodunuz: {code}\nKod tek kullanimliktir."
                };

                using var client = new SmtpClient();
                await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(username, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthNotificationService:SendOtpCode] Exception: {ex.Message}");
                if (allowConsoleFallback)
                {
                    Console.WriteLine($"[OTP:FALLBACK] {email} icin kod: {code}");
                    return true;
                }
                return false;
            }
        }
    }
}
