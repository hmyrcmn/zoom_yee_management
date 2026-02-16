using Toplanti.Core.Entities.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Core.Utilities.Security.JWT
{
    public interface ITokenHelper
    {
        AccessToken CreateZoomToken();
        Task<string> CreateAccessToken();
        AccessToken CreateToken(User user, List<OperationClaim> operationClaims);
    }
}
