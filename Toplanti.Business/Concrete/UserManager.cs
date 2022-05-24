using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toplanti.Business.Abstract;
using Toplanti.Core.Utilities.Helper;

namespace Toplanti.Business.Concrete
{
    public class UserManager : IUserService
    {
        public List<string> RoleName()
        {
            string roleName = "";

            roleName = new UserCookie().Rol();

            var list = roleName.Split(",").ToList();

            List<string> oysRoleList = new List<string>();

            for (int i = 1; i < list.Count(); i += 2)
            {
                if (list[i - 1] == "1032")
                {
                    oysRoleList.Add(list[i]);
                }
            }

            return oysRoleList;
        }
    }
}
