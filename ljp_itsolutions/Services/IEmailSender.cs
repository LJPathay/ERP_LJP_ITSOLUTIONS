using System.Threading.Tasks;

namespace ljp_itsolutions.Services
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string to, string subject, string body);
    }
}
