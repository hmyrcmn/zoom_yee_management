using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Entities.Zoom
{
    public class UserMeetings
    {
        public string uuid { get; set; }
        public string id { get; set; }
        public string host_id { get; set; }
        public string topic { get; set; }
        public string agenda { get; set; }
        public string type { get; set; }
        public DateTime start_time { get; set; }
        public DateTime created_at { get; set; }
        public int duration { get; set; }
        public string timezone { get; set; }
        public string join_url { get; set; }
    }
}
