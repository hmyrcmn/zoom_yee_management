using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Entities.Zoom
{
    public class ZoomUserList
    {
        public IList<ZoomUsers> users { get; set; }
        public IList<UserMeetings> meetings { get; set; }
    }
}
