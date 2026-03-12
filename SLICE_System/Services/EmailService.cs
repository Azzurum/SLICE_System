using System;
using System.Net;
using System.Net.Mail;

namespace SLICE_System.Services
{
    public class EmailService
    {
        // IMPORTANT: Replace these with your actual system email and app password
        private readonly string _systemEmail = "slice.automated@gmail.com";
        private readonly string _appPassword = "sbwzycmywldszfof";

        public bool SendPasswordResetEmail(string targetEmail, string resetCode)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(_systemEmail, _appPassword),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_systemEmail, "S.L.I.C.E. System Admin"),
                    Subject = "Security Alert: Password Reset Code",
                    Body = $@"
                        <div style='font-family: Arial, sans-serif; color: #2C3E50;'>
                            <h2>Password Reset Request</h2>
                            <p>We received a request to reset your password for the S.L.I.C.E. POS System.</p>
                            <p>Your 6-digit verification code is:</p>
                            <h1 style='color: #E74C3C; letter-spacing: 5px;'>{resetCode}</h1>
                            <p><i>This code will expire in 15 minutes. If you did not request this, please ignore this email.</i></p>
                        </div>",
                    IsBodyHtml = true,
                };
                mailMessage.To.Add(targetEmail);

                smtpClient.Send(mailMessage);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}