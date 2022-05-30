using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Business.Helpers
{
    public interface IEmailHelper
    {
        public bool OpenedZoom(string email, string zoomId, int type);
    }
}
