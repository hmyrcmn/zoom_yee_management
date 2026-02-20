using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Toplanti.Entities.DTOs
{
    public class ZoomCreateUserRequest
    {
        [JsonPropertyName("action")]
        public string action { get; set; }

        [JsonPropertyName("user_info")]
        public ZoomInviteUserInfo user_info { get; set; }
    }

    public class ZoomInviteUserInfo
    {
        [JsonPropertyName("email")]
        public string email { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public int type { get; set; } = 1;

        [JsonPropertyName("first_name")]
        public string first_name { get; set; } = string.Empty;

        [JsonPropertyName("last_name")]
        public string last_name { get; set; } = string.Empty;
    }
}
