using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Entities.Zoom
{
    public class PastMeetingDetails
    {
        public string uuid { get; set; }
        public long id { get; set; }
        public string host_id { get; set; }
        public int type { get; set; }
        public string topic { get; set; }
        public string user_name { get; set; }
        public string user_email { get; set; }
        public string host_email { get; set; }
        public DateTime start_time { get; set; }
        public DateTime end_time { get; set; }
        public int duration { get; set; }
        public int total_minutes { get; set; }
        public int participants_count { get; set; }
        public string dept { get; set; }
        public string source { get; set; }

        public string start_url {get;set;}
        public string join_url { get;set;}

    }
}
