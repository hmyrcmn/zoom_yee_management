using System.Collections.Generic;
using System.Threading.Tasks;
using Toplanti.Core.Utilities.Results;
using Toplanti.Entities.DTOs;
using Toplanti.Entities.Zoom;

namespace Toplanti.Business.Abstract
{
    public interface IZoomService
    {
        Task<IDataResult<List<ZoomUsers>>> GetWorkspaceUsers();
        Task<bool> IsUserActiveInZoom(string email);
        Task<IResult> AddUserToZoom(ZoomUserCreatedResponse request);
        Task<IResult> ProvisionUserForActivation(ZoomUserCreatedResponse request);
        Task<IResult> DeleteUserFromZoom(string email);
        Task<IResult> DeleteUsersFromZoom(List<string> emails);
    }
}
