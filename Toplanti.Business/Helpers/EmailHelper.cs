using MailKit.Net.Smtp;
using MimeKit;
using System;

namespace Toplanti.Business.Helpers
{
    internal class EmailHelper : IEmailHelper
    {
        public bool OpenedZoom(string email, string zoomId, int type)
        {
            try
            {
                SmtpClient client = new SmtpClient();
                client.Connect("smtp.elasticemail.com", 2525, MailKit.Security.SecureSocketOptions.StartTls);
                client.Authenticate("oys.bilgi@yee.org.tr", "07FB10924BFF74BD4DF6CBDE2ED01AF92036");

                MimeMessage message = new MimeMessage();
                message.Headers.Add("IsTransactional", "True");

                MailboxAddress from = new MailboxAddress("Bilgilendirme", "oys.bilgi@yee.org.tr");
                message.From.Add(from);

                MailboxAddress to = new MailboxAddress("Kullanıcı", "burak.acar@yee.org.tr");
                message.To.Add(to);

                BodyBuilder bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = email + " kullanıcısı olarak  " + zoomId + "  ID ile tipi: " + type + " lisansıyla toplantı oluşturuldu.";
                bodyBuilder.TextBody = "Yunus Emre Enstitüsü";

                message.Body = bodyBuilder.ToMessageBody();
                message.Subject = "Zoom Toplantısı oluşturuldu.";

                client.Send(message);
                client.Disconnect(true);
                client.Dispose();

                return true;
            }
            catch (Exception ex)
            {

                return false;
            }
        }
    }
}
