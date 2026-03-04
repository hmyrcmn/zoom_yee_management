using System.Threading.Tasks;

namespace Toplanti.Business.Abstract
{
    public interface IAuthNotificationService
    {
        Task<bool> SendOtpCode(string email, string code);
    }
}
