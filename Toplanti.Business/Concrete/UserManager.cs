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
            var roleName = new UserCookie().Rol();

            var list = roleName.Split(",").ToList();
            
            Dictionary<string, string> listRole = new Dictionary<string, string>();

            int sayac = 0;

            for (int i = 0; i < list.Count() / 2; i++)
            {
                listRole.Add(list[sayac], list[sayac + 1]);
                sayac = +2;
            }

            List<string> oysRoleList = new List<string>();
            foreach (var item in listRole)
            {
                if (item.Key == "1032")//cevrimici rol id seçildi
                {
                    oysRoleList.Add(item.Value);
                }
            }
            return oysRoleList;
        }
    }
}
