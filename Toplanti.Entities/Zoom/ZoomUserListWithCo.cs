using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Entities.Zoom
{
    public class ZoomUserListWithCo : ZoomCo
    {
        public List<ZoomUsers> users { get; set; }
    }
}
