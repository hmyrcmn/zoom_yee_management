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
        public IList<Participants> participants { get; set; }
        public int page_count { get; set; }
        public int page_number { get; set; }
        public int page_size { get; set; }
        public int total_records { get; set; }
    }
}
