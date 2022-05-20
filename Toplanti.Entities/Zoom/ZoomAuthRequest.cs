using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Entities.Zoom
{
    public class ZoomAuthRequest
    {
        public string grant_type { get; set; }
        public string code { get; set; }
        public string redirect_url { get; set; }
        public string client_id { get; set; }
        public string client_secret { get; set; }
        public string access_token { get; set; }
    }
}
