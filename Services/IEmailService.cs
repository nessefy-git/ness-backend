using System.Net.Mail;
using System.Net;

namespace SocialMediaAuthAPI.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }


}
