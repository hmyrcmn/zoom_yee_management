using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Toplanti.Entities.Zoom
{
    public class ZoomUsers
    {
        public string id { get; set; }
        [JsonPropertyName("first_name")]
        public string first_name { get; set; }

        [JsonPropertyName("last_name")]
        public string last_name { get; set; }
        public string email { get; set; }
        public int type { get; set; }
        [JsonPropertyName("pmi")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long? pmi { get; set; }
        public string timezone { get; set; }
        public int verified { get; set; }
        public string dept { get; set; }
        public DateTime created_at { get; set; }
        public DateTime last_login_time { get; set; }
        public string last_client_version { get; set; }
        public string language { get; set; }
        public string phone_number { get; set; }
        public string status { get; set; }
        public string role_id { get; set; }
    }
}
