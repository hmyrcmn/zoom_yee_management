using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Entities.Zoom
{
    public partial class ZoomCreateRequest
    {
        public string? agenda { get; set; }
        public bool default_password { get; set; }
        public int duration { get; set; }
        public string? password { get; set; }
        public bool pre_schedule { get; set; }
        public string? schedule_for { get; set; }
        public DateTime start_time { get; set; }
        public string? template_id { get; set; }
        public string? timezone { get; set; }
        public string? topic { get; set; }
        public int type { get; set; }
        public int ClassId { get; set; }
        public Settings? settings { get; set; }
    }

    public class Settings
    {
        public bool host_video { get; set; } = true;
        public bool participant_video { get; set; } = true;
        public bool join_before_host { get; set; } = true;
        public bool mute_upon_entry { get; set; } = true;
        public breakout_room breakout_room { get; set; }
        public Settings()
        {
            breakout_room = new breakout_room();
        }
    }

    public class breakout_room
    {
        public bool enable { get; set; }=true;
    }
}

