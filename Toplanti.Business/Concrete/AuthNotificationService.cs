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
                message.Subject = "YEE Toplantı Giriş Doğrulama Kodu";

                var builder = new BodyBuilder
                {
                    TextBody = $"Giriş doğrulama kodunuz: {code}\nKod tek kullanımlıktır.",
                    HtmlBody = $@"
                        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 8px;'>
                            <h2 style='color: #2c3e50; text-align: center;'>YEE Toplantı Sistemi</h2>
                            <p style='font-size: 16px; color: #333;'>Merhaba,</p>
                            <p style='font-size: 16px; color: #333;'>Sisteme giriş yapabilmeniz için tek kullanımlık doğrulama kodunuz aşağıdadır:</p>
                            <div style='text-align: center; margin: 30px 0;'>
                                <span style='font-size: 32px; font-weight: bold; color: #e74c3c; letter-spacing: 5px; background: #f9f9f9; padding: 10px 20px; border-radius: 5px; display: inline-block;'>{code}</span>
                            </div>
                            <p style='font-size: 14px; color: #7f8c8d; text-align: center; border-top: 1px solid #eee; padding-top: 15px;'>
                                <em>Bu kod tek kullanımlıktır. Lütfen kodunuzu kimseyle paylaşmayınız.</em>
                            </p>
                        </div>"
                };

                message.Body = builder.ToMessageBody();

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
