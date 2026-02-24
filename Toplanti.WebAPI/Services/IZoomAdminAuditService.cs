using System.Threading.Tasks;

namespace Toplanti.WebAPI.Services
{
    public interface IZoomAdminAuditService
    {
        Task LogAsync(string? adminEmail, string actionType, string? targetEmails, string result, string? message);
    }
}

